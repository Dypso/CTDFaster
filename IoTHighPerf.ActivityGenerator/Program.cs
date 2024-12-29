using FASTER.core;
using IoTHighPerf.ActivityGenerator.Models;
using IoTHighPerf.ActivityGenerator.Services;
using IoTHighPerf.Core.Interfaces;
using IoTHighPerf.Infrastructure.PubSub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime;
using Microsoft.Extensions.Options;  

namespace IoTHighPerf.ActivityGenerator;

public class Program
{
    public static async Task Main(string[] args)
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var config = context.Configuration;
                
                // Configuration du service
                var activityOptions = new ActivityFileOptions();
                config.GetSection("ActivityFile").Bind(activityOptions);
                services.Configure<ActivityFileOptions>(config.GetSection("ActivityFile"));
                
                // Création des répertoires
                EnsureDirectoriesExist(activityOptions);

                // Configuration de FASTER
                var fasterConfig = new FasterConfig();
                config.GetSection("FasterPubSub").Bind(fasterConfig);
                services.AddSingleton(fasterConfig);
                
                // FASTER Device et Log
                services.AddSingleton<IDevice>(sp =>
                {
                    Directory.CreateDirectory(fasterConfig.LogPath);
                    return Devices.CreateLogDevice(Path.Combine(fasterConfig.LogPath, "pubsub.log"));
                });

                services.AddSingleton<FasterLog>(sp =>
                {
                    var device = sp.GetRequiredService<IDevice>();
                    return new FasterLog(new FasterLogSettings
                    {
                        LogDevice = device,
                        PageSize = fasterConfig.PageSize,
                        SegmentSize = fasterConfig.SegmentSize,
                        MemorySize = fasterConfig.MemorySize,
                    });
                });

                // Services métier
                services.AddSingleton<ICounterManager>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<CounterManager>>();
                    return new CounterManager(activityOptions.CounterFilePath, logger);
                });

                services.AddSingleton<IActivityFileWriter>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<ActivityFileOptions>>();
                    var counterManager = sp.GetRequiredService<ICounterManager>();
                    return new ActivityFileWriter(options, counterManager);
                });
                
                // FASTER PubSub
                services.AddSingleton<IFasterSubscriber<byte[]>>(sp =>
                {
                    var fasterLog = sp.GetRequiredService<FasterLog>();
                    var logger = sp.GetRequiredService<ILogger<FasterSubscriber<byte[]>>>();
                    return new FasterSubscriber<byte[]>(fasterLog, logger);
                });

                // Service principal
                services.AddHostedService<ReactiveActivityGenerator>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                
                if (!context.HostingEnvironment.IsDevelopment())
                {
                    logging.AddFilter("Microsoft", LogLevel.Warning);
                    logging.AddFilter("System", LogLevel.Warning);
                }
            })
            .Build();

        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "Application stopped unexpectedly");
            throw;
        }
    }

    private static void EnsureDirectoriesExist(ActivityFileOptions options)
    {
        try
        {
            Directory.CreateDirectory(options.OutputPath);
            Directory.CreateDirectory(options.TempPath);
            Directory.CreateDirectory(Path.GetDirectoryName(options.CounterFilePath)!);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create required directories", ex);
        }
    }
}

public class FasterConfig
{
    public string LogPath { get; set; } = "logs/faster";
    public int PageSize { get; set; } = 4096;
    public int SegmentSize { get; set; } = 1048576;    // 1MB
    public long MemorySize { get; set; } = 2097152;    // 2MB
}