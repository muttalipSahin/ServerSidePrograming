using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace ServerSidePrograming
{
    internal class Program
    {
        static void Main(string[] args)
        {
            

            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(services =>
                {
                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.ConfigureFunctionsApplicationInsights();
                    services.AddLogging(loggingBuilder =>
                    {
                        loggingBuilder.AddConsole();
                        loggingBuilder.AddApplicationInsights();
                    });
                    services.AddSingleton(new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")));
                })
                .Build();

            host.Run();

        }
    }
}