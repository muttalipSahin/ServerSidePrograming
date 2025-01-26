using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ServerSidePrograming
{
    public class ProcessWeatherJobFunction
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILogger _logger;
        private const string BuienraderApiUrl = "https://data.buienradar.nl/2.0/feed/json";

        // Constructor injection to ensure logger is properly initialized
        public ProcessWeatherJobFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessWeatherJobFunction>();
        }

        [Function("ProcessWeatherJob")]
        [QueueOutput("image-processing-queue")]
        public async Task<string> ProcessWeatherJob(
            [QueueTrigger("weather-job-queue")] string jobId)
        {
            _logger.LogInformation($"Processing job with ID: {jobId}");

            var response = await _httpClient.GetStringAsync(BuienraderApiUrl);
            _logger.LogInformation("Buienradar API response received.");

            var weatherData = JsonDocument.Parse(response).RootElement;

            if (!weatherData.TryGetProperty("actual", out JsonElement actualElement) ||
                !actualElement.TryGetProperty("stationmeasurements", out JsonElement stationsElement))
            {
                _logger.LogError("Missing 'actual' or 'stationmeasurements' in the JSON response.");
                throw new KeyNotFoundException("Missing 'actual' or 'stationmeasurements' in the JSON response.");
            }

            var weatherList = new List<object>();

            foreach (var station in stationsElement.EnumerateArray())
            {
                string stationName = station.TryGetProperty("stationname", out JsonElement nameElement)
                    ? nameElement.GetString() ?? "Unknown Station"
                    : "Unknown Station";

                string temperature = "N/A";
                if (station.TryGetProperty("temperature", out JsonElement tempElement))
                {
                    temperature = tempElement.ValueKind == JsonValueKind.Number
                        ? tempElement.GetDouble().ToString("0.0")
                        : tempElement.GetString() ?? "N/A";
                }

                string weatherText = $"{stationName}: {temperature}°C";
                weatherList.Add(new { jobId, stationName, temperature, weatherText });
            }

            _logger.LogInformation($"Processed {weatherList.Count} weather stations for job {jobId}.");

            return JsonSerializer.Serialize(weatherList);
        }

        /*[Function("ProcessWeatherJob")]
        [QueueOutput("image-processing-queue")]
        public async Task<string> ProcessWeatherJob(
        [QueueTrigger("weather-job-queue")] string jobId)
        {
            _logger.LogInformation($"Processing job with ID: {jobId}");

            var response = await _httpClient.GetStringAsync(BuienraderApiUrl);
            _logger.LogInformation("Buienradar API response received.");

            var weatherData = JsonDocument.Parse(response).RootElement;

            if (!weatherData.TryGetProperty("actual", out JsonElement actualElement) ||
                !actualElement.TryGetProperty("stationmeasurements", out JsonElement stationsElement))
            {
                _logger.LogError("Missing 'actual' or 'stationmeasurements' in the JSON response.");
                throw new KeyNotFoundException("Missing 'actual' or 'stationmeasurements' in the JSON response.");
            }

            _logger.LogInformation($"Total weather stations found: {stationsElement.GetArrayLength()}");

            // Process only the first station for debugging
            var firstStation = stationsElement.EnumerateArray().FirstOrDefault();

            if (firstStation.ValueKind == JsonValueKind.Undefined)
            {
                _logger.LogWarning("No stations found in the response.");
                return JsonSerializer.Serialize(new { jobId, error = "No station data available" });
            }

            string stationName = firstStation.TryGetProperty("stationname", out JsonElement nameElement)
                ? nameElement.GetString() ?? "Unknown Station"
                : "Unknown Station";

            string temperature = "N/A";
            if (firstStation.TryGetProperty("temperature", out JsonElement tempElement))
            {
                temperature = tempElement.ValueKind == JsonValueKind.Number
                    ? tempElement.GetDouble().ToString("0.0")
                    : tempElement.GetString() ?? "N/A";
            }

            string weatherText = $"{stationName}: {temperature}°C";

            var result = new
            {
                jobId,
                stationName,
                temperature,
                weatherText
            };

            _logger.LogInformation($"Processed station: {stationName}, Temperature: {temperature}°C");

            return JsonSerializer.Serialize(result);
        }*/

    }
}
