using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Drawing;
using ServerSidePrograming;

namespace ServerSideProgramming
{
    public class GenerateWeatherImageFunction
    {
        private readonly ILogger<GenerateWeatherImageFunction> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private static readonly HttpClient _httpClient = new HttpClient();

        //private const string UnsplashApiUrl = "https://api.unsplash.com/photos/random?query=weather&client_id=7kSVaZfdapt6ios1FqvWsLwXuGJfjmG009iCvQPDKt4";
        //private const string UnsplashApiUrl = "https://api.unsplash.com/photos/random?query=weather&client_id=2Lh2frmnmzbSul3lPStuW_P6_sLMEEAGtrV9oz4MIWQ";
        //private const string UnsplashApiUrl = "https://api.unsplash.com/photos/random?query=weather&client_id=kla49dlH-AmOvOs_NfKZJuAR_noD2LXDlLjU3w_3INk";

        private readonly string UnsplashApiUrl = "https://api.unsplash.com/photos/random?query=weather&client_id=";
        private readonly string _unsplashApiKey;

        public GenerateWeatherImageFunction(BlobServiceClient blobServiceClient, ILogger<GenerateWeatherImageFunction> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _unsplashApiKey = Environment.GetEnvironmentVariable("unsplash1");
            UnsplashApiUrl = UnsplashApiUrl + _unsplashApiKey;
        }

        [Function("GenerateWeatherImage")]
        public async Task GenerateWeatherImage([QueueTrigger("image-processing-queue")] string message)
        {
            _logger.LogInformation("Received message from queue: {message}", message);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            var requestData = JsonSerializer.Deserialize<WeatherImageRequest>(message, options);
            if (requestData == null || string.IsNullOrWhiteSpace(requestData.JobId) || string.IsNullOrWhiteSpace(requestData.StationName) || string.IsNullOrWhiteSpace(requestData.WeatherText))
            {
                _logger.LogError("Invalid message format or missing fields.");
                return;
            }

            try
            {
                _logger.LogInformation("Fetching image from Unsplash...");
                var unsplashResponse = await _httpClient.GetStringAsync(UnsplashApiUrl);
                var unsplashData = JsonDocument.Parse(unsplashResponse).RootElement;

                if (!unsplashData.TryGetProperty("urls", out JsonElement urlsElement) ||
                    !urlsElement.TryGetProperty("full", out JsonElement imageUrlElement))
                {
                    _logger.LogError("Failed to retrieve image URL from Unsplash API.");
                    return;
                }

                string imageUrl = imageUrlElement.GetString();
                _logger.LogInformation($"Image URL fetched: {imageUrl}");

                string localImagePath = Path.Combine(Path.GetTempPath(), $"{requestData.JobId}_{requestData.StationName}_temp.png");
                using (var imageResponse = await _httpClient.GetAsync(imageUrl))
                using (var fs = new FileStream(localImagePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await imageResponse.Content.CopyToAsync(fs);
                }
                _logger.LogInformation("Image downloaded successfully.");

                string processedImagePath = WriteTextOnImage(requestData.JobId, requestData.StationName, requestData.WeatherText, localImagePath);
                _logger.LogInformation($"Image processed with text overlay: {processedImagePath}");

                var blobClient = _blobServiceClient.GetBlobContainerClient("weather-images")
                                                   .GetBlobClient($"{requestData.JobId}_{requestData.StationName}.png");
                using (var fileStream = new FileStream(processedImagePath, FileMode.Open, FileAccess.Read))
                {
                    await blobClient.UploadAsync(fileStream, overwrite: true);
                }

                _logger.LogInformation("Processed image uploaded to Blob Storage.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing image: {ex.Message}");
            }
        }

        private static string WriteTextOnImage(string jobId, string stationName, string weatherText, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(jobId) || string.IsNullOrWhiteSpace(stationName) || string.IsNullOrWhiteSpace(weatherText))
            {
                throw new ArgumentException("Invalid data for image processing.");
            }

            string modifiedImagePath = Path.Combine(Path.GetTempPath(), $"{jobId}_{stationName}.png");

            using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
            using (var bitmap = new Bitmap(fileStream))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                graphics.DrawString($"{weatherText}", new Font("Arial", 120, FontStyle.Bold), Brushes.Black, new PointF(12, 12));
                bitmap.Save(modifiedImagePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            return modifiedImagePath;
        }
    }

}
