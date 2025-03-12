using Dapper;
using GenerateTaxNew.Models;
using GenerateTaxNew.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GenerateTaxNew
{
    class Program
    {
        private static IConfiguration Configuration { get; set; }

        static async Task Main(string[] args)
        {
            // Setup configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();

            // Setup Serilog
            var logFileName = $"log-{DateTime.Now:yyyyMMdd}.txt";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "logs", logFileName))
                .CreateLogger();

            // Use Serilog for logging
            var loggerFactory = LoggerFactory.Create(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(Log.Logger);
            });

            var logger = loggerFactory.CreateLogger<GenerateTaxService>();

            logger.LogInformation("Starting application...");

            GenerateTaxService _service = new GenerateTaxService(logger);
            string buCode = configuration["AppConfig:BuCode"];
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var connection = configuration.GetConnectionString("DevConnection");
            var RetryCount = configuration.GetConnectionString("RetryCount");
            try
            {
                //
                    await _service.GenerateTaxFunctionAsync(buCode, connectionString,Convert.ToInt32(RetryCount));
                //}
               
            }
            catch(Exception ex)
            {
                logger.LogInformation("Error Generate : " + ex.Message);
            }
            

            Log.CloseAndFlush();
        }
    }
}
