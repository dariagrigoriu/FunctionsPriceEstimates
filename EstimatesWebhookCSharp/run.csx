#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.WindowsAzure.Storage.Table;

//App Insights integration
private static TelemetryClient telemetry = new TelemetryClient();
//private static string key = TelemetryConfiguration.Active.InstrumentationKey = System.Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", EnvironmentVariableTarget.Process);

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req,IQueryable<PriceModel> RealEstateEstimateModel, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    telemetry.TrackEvent("Function Started");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    String marketParam = data.market;

    PriceModelData marketModel = null;
    foreach (PriceModel model in RealEstateEstimateModel.Where(p => p.market == marketParam).ToList())
    {
        log.Info($"Name: {model.Name}");
        marketModel  = model.getModel();
    }


    if (marketModel == null) {
        var response2 = req.CreateResponse(HttpStatusCode.NotFound, new {Error = "not such market" });
        response2.Content = new StringContent("angular.callbacks._0({\"Error\":\"not such market:"+ marketParam +"\"})"); 
 
        return response2;
    }

    using (var operation = telemetry.StartOperation<RequestTelemetry>("House Price Estimate Computation")) 
    { 
        float[] input = null;
        //using (var parsingOperation = telemetry.StartOperation<RequestTelemetry>("House Price Estimate Computation")) 
        //{
            input = await parseInput(data);
           // parsingOperation.Telemetry.ResponseCode = "200";
            //telemetry.StopOperation(parsingOperation); 
        //}

        double[] coef = marketModel.coef;//getRegressionCoeficent();
        
        double intercept = marketModel.intercept;//getIntercept();
        
        double[] mean = marketModel.mean;//getMetricsMean();
        
        double[] sig = marketModel.magnitude;//getMatricsMagnitude();
        
        double result = intercept;
        
        for(int i = 0 ; i < 6; i++){
            result += coef[i]*(input[i] - mean[i])/sig[i];
        }
        var response = req.CreateResponse(HttpStatusCode.OK, new {EstimatedPrice = result });
        if(req.Method == HttpMethod.Get) { 
            response.Content = new StringContent("angular.callbacks._0({\"EstimatedPrice\":"+result.ToString()+"})"); 
        }
        operation.Telemetry.ResponseCode = "200";
        telemetry.StopOperation(operation); 
        return response;
    } 

}

public static async Task<float[]> parseInput(dynamic data) {

    // parse query parameter
        var rooms = data.rooms.ToString();
        
        var baths = data.baths.ToString();

        var sq = data.sq.ToString();

        var lot = data.lot.ToString();

        var year = data.year.ToString();

        var days = data.days.ToString();

        float[] input = new float[] {
            Single.Parse(rooms), 
            Single.Parse(baths), 
            Single.Parse(sq),
            Single.Parse(lot), 
            Single.Parse(year), 
            Single.Parse(days)};
        return input;

}


public class PriceModel : TableEntity
{
    public string Name { get; set; }

    public double Intercept {get;set;}

    public float[] RegressionCoef {get;set;}

    public String market {get;set;}

    public String ModelStr{get;set;}

    public PriceModelData getModel() {
        return JsonConvert.DeserializeObject<PriceModelData>(this.ModelStr);
    }
}

public class PriceModelData 
{
    public double[] coef {get;set;}

    public double[] mean {get;set;}
    
    public double[] magnitude {get;set;}

    public double intercept{get;set;}
    
}