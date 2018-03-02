#r "Microsoft.Azure.WebJobs.Extensions.EventGrid"
#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"

using System.Web;
using System.Text;
using System.Json;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Host.Bindings.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

public static async Task Run(Stream myBlob, EventGridEvent myEvent, TraceWriter log)
{
    // Pull relevant information from App Settings
    string containerSAS = System.Environment.GetEnvironmentVariable("describephoto_container_SAS", EnvironmentVariableTarget.Process);
    string fullObjectPath = (string)myEvent.Data["url"] + containerSAS;

    // Caption the photo
    string caption = await MakeAnalysisRequest(fullObjectPath);

    // Save the caption as blob metadata
    CloudBlockBlob myImage = new CloudBlockBlob(new Uri(fullObjectPath));
    await myImage.FetchAttributesAsync();
    if (!myImage.Metadata.ContainsKey("caption"))
    {
        myImage.Metadata.Add("caption", caption);
        await myImage.SetMetadataAsync();
        log.Info($"Found new/updated blob\n Path: \"{fullObjectPath}\"\n Caption: \"{caption}\"");
    }
    else
    {
        log.Info($"Blob is already captioned\n Path: \"{fullObjectPath}\"\n Caption: \"{caption}\"");
    }
}

/// <summary>
/// Gets the analysis of the specified image file by using the Computer Vision REST API.
/// </summary>
/// <param name="imageFilePath">The image file.</param>
static async Task<string> MakeAnalysisRequest(string imageFilePath)
{
    // NOTE: Free trial vision subscription keys are generated in the westcentralus region, so if you are using
    // a free trial subscription key, you should not need to change the region.
    string uri = "https://westcentralus.api.cognitive.microsoft.com/vision/v1.0/describe";
    string visionSubscriptionKey = System.Environment.GetEnvironmentVariable("VisionSubscriptionKey", EnvironmentVariableTarget.Process);

    HttpClient client = new HttpClient();

    // Request headers
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", visionSubscriptionKey);

    HttpResponseMessage response;

    // Prepare the request content
    JsonObject jsonContent = new JsonObject();
    jsonContent.Add("url", imageFilePath);
    StringContent content = new StringContent(jsonContent.ToString(), Encoding.UTF8, "application/json");

    // Execute the REST API call
    response = await client.PostAsync(uri, content);

    if (response.IsSuccessStatusCode)
    {

        // Get and parse the JSON response
        string responseString = await response.Content.ReadAsStringAsync();
        JObject responseJson = JObject.Parse(responseString);
        return (string)(responseJson["description"]["captions"][0]["text"]);
    }
    return "could not caption";
}