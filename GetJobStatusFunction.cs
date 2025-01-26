using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;

using Azure.Data.Tables;
using Azure;

namespace ServerSidePrograming
{
    public class GetJobStatusFunction
    {
        private readonly ILogger _logger;
        private readonly BlobServiceClient _blobServiceClient;

        public GetJobStatusFunction(ILoggerFactory loggerFactory, BlobServiceClient blobServiceClient)
        {
            _logger = loggerFactory.CreateLogger<GetJobStatusFunction>();
            _blobServiceClient = blobServiceClient;
        }


        [Function("GetJobStatus")]
        public async Task<HttpResponseData> GetJobStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobstatus/{guid}")] HttpRequestData req, Guid guid)
        {
            var tableClient = new TableClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "jobstatus");
            await tableClient.CreateIfNotExistsAsync();

            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("weather-images");

            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "application/json");

            try
            {
                var entity = await tableClient.GetEntityIfExistsAsync<TableEntity>("JobStatus", guid.ToString());
                if (entity.HasValue)
                {
                    var status = entity.Value["STATUS"]?.ToString() ?? "UNKNOWN";

                    
                    int blobCount = 0;
                    await foreach (var blob in blobContainerClient.GetBlobsAsync())
                    {
                        if (blob.Name.Contains(guid.ToString()))
                        {
                            blobCount++;
                        }
                    }

                    if (blobCount >= 40)
                    {
                        status = "FINISHED";
                        entity.Value["STATUS"] = status;
                        await tableClient.UpdateEntityAsync(entity.Value, ETag.All);
                    }

                    response.StatusCode = HttpStatusCode.OK;
                    var result = new { jobId = guid.ToString(), STATUS = status, blobCount };
                    await response.WriteStringAsync(JsonSerializer.Serialize(result));
                }
                else
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    var result = new { message = "Job not found or has just started. Please wait a little bit." };
                    await response.WriteStringAsync(JsonSerializer.Serialize(result));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving job status: {ex.Message}");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync(JsonSerializer.Serialize(new { message = "Error retrieving job status." }));
            }

            return response;
        }
    }
}
