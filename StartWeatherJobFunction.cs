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
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req)
        {
            string jobId = Guid.NewGuid().ToString();

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { jobId }));

            _logger.LogInformation($"Job {jobId} added to queue.");

            return jobId;
        }
    }
}
