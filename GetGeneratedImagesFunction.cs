using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;

namespace ServerSidePrograming
{
    public class GetGeneratedImagesFunction
    {
        private readonly BlobServiceClient _blobServiceClient;

        public GetGeneratedImagesFunction(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }

        [Function("GetGeneratedImages")]
        public async Task<HttpResponseData> GetGeneratedImages(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "images/{guid}")] HttpRequestData req, Guid guid)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("weather-images");
            var blobs = containerClient.GetBlobs();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var matchingBlobs = blobs
                .Where(blob => blob.Name.Contains(guid.ToString()))
                .Select(blob => $"{containerClient.Uri}/{blob.Name}");

            await response.WriteStringAsync(JsonSerializer.Serialize(matchingBlobs));

            return response;
        }
    }
}
