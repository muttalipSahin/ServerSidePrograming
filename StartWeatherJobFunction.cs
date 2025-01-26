using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace ServerSidePrograming
{
    public class StartWeatherJobFunction
    {
        private readonly ILogger<StartWeatherJobFunction> _logger;

        public StartWeatherJobFunction(ILogger<StartWeatherJobFunction> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("StartWeatherJob")]
        [QueueOutput("weather-job-queue")]
        public async Task<string> StartWeatherJob(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
        {
            string jobId = Guid.NewGuid().ToString();
            var tableClient = new TableClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "jobstatus");
            await tableClient.CreateIfNotExistsAsync();

            var entity = new TableEntity("JobStatus", jobId)
    {
            { "STATUS", "PENDING" },
            { "CreatedTime", DateTime.UtcNow.ToString("o") }
    };
            await tableClient.AddEntityAsync(entity);

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { jobId }));

            _logger.LogInformation($"Job {jobId} added to queue with status PENDING.");

            return jobId;
        }

    }
}
