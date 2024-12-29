using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using static Microsoft.AspNetCore.Http.Results; 
using Microsoft.AspNetCore.Server.Kestrel.Core;
using IoTHighPerf.Core.Interfaces;
using IoTHighPerf.Infrastructure.Storage;
using IoTHighPerf.Infrastructure.Audit;
using IoTHighPerf.Core.Models;
using System.Threading.Channels;
using System.Collections.Generic;

using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;


using System.Text;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using System.Net;


using IoTHighPerf.Core.Services;
using IoTHighPerf.Infrastructure.ZeroMQ;



namespace IoTHighPerf.Api;

public static class TimeServiceInit
{
    private const int MaxJsonResponseSize = 256; // Taille maximale d'une réponse JSON
    private static long _currentTicks;
    private static readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(3));
    private static readonly Channel<(string deviceId, TaskCompletionSource<byte[]>)> _timeRequests = 
        Channel.CreateUnbounded<(string deviceId, TaskCompletionSource<byte[]>)>(
            new UnboundedChannelOptions { SingleReader = true });

    private static readonly byte[] _jsonPrefix = Encoding.UTF8.GetBytes("{\"time\":");
    private static readonly byte[] _jsonSuffix = Encoding.UTF8.GetBytes("}");

    /// <summary>
    /// Met à jour l'heure actuelle à intervalles réguliers.
    /// </summary>
    public static async Task UpdateTimeAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            Interlocked.Exchange(ref _currentTicks, DateTime.UtcNow.Ticks);
        }
    }

    /// <summary>
    /// Traitement des requêtes en mode batch.
    /// </summary>
    public static async Task ProcessTimeRequestsBatch(IAuditLogger audit)
    {
        const int MaxBatchSize = 5000;
        const int IdleDelayMs = 1; // Pause en cas d'inactivité
        var batch = new List<(string deviceId, TaskCompletionSource<byte[]>)>(MaxBatchSize);
        var responseBuffer = new byte[MaxJsonResponseSize];

        while (true)
        {
            batch.Clear();
            var deadline = DateTime.UtcNow.AddMicroseconds(500);

            // Collecter les requêtes jusqu'à la deadline ou la taille max
            while (DateTime.UtcNow < deadline && batch.Count < MaxBatchSize)
            {
                if (await _timeRequests.Reader.WaitToReadAsync())
                {
                    while (batch.Count < MaxBatchSize && _timeRequests.Reader.TryRead(out var request))
                    {
                        batch.Add(request);
                    }
                }
            }

            if (batch.Count > 0)
            {
                var currentTime = Interlocked.Read(ref _currentTicks);

                // Audits en parallèle (conversion explicite des ValueTask en Task)
                var auditTasks = batch.Select(request => audit.LogTimeSyncAsync(request.deviceId).AsTask()).ToArray();
                await Task.WhenAll(auditTasks);

                // Génération des réponses JSON
                foreach (var (deviceId, tcs) in batch)
                {
                    BuildJsonResponse(currentTime, deviceId, responseBuffer, out var responseLength);

                    // Copier les données pour éviter tout conflit avec d'autres requêtes
                    var responseCopy = new byte[responseLength];
                    Buffer.BlockCopy(responseBuffer, 0, responseCopy, 0, responseLength);
                    tcs.TrySetResult(responseCopy);
                }
            }
            else
            {
                // Réduire la charge CPU en cas d'inactivité
                await Task.Delay(IdleDelayMs);
            }
        }
    }

    /// <summary>
    /// Ajoute une requête dans le canal de traitement batché.
    /// </summary>
    public static async Task<byte[]> GetBatchedTimeAsync(string deviceId)
    {
        var tcs = new TaskCompletionSource<byte[]>();
        await _timeRequests.Writer.WriteAsync((deviceId, tcs));
        return await tcs.Task;
    }

    /// <summary>
    /// Récupère les ticks actuels (synchronisé avec l'horloge).
    /// </summary>
    public static long GetCurrentTicks() => _currentTicks;

    /// <summary>
    /// Génère une réponse JSON optimisée directement dans un buffer.
    /// </summary>
    public static void BuildJsonResponse(long currentTime, string deviceId, byte[] buffer, out int length)
    {
        int offset = 0;

        // Écrire le préfixe JSON
        Buffer.BlockCopy(_jsonPrefix, 0, buffer, offset, _jsonPrefix.Length);
        offset += _jsonPrefix.Length;

        // Convertir le temps en UTF-8
        var timeStr = currentTime.ToString();
        var timeLength = Encoding.UTF8.GetBytes(timeStr, 0, timeStr.Length, buffer, offset);
        offset += timeLength;

        // Ajouter le suffixe JSON
        Buffer.BlockCopy(_jsonSuffix, 0, buffer, offset, _jsonSuffix.Length);
        offset += _jsonSuffix.Length;

        // Longueur totale de la réponse
        length = offset;
    }



        /// <summary>
    /// Génère une réponse JSON optimisée.
    /// </summary>
    public static void BuildJsonResponseOptimise (long currentTime, Span<byte> buffer, out int length)
    {

        #region usage
                // A APPELER NORMALEMENT ANSI :
                // Ce qui manquerait c'est peut être descende l'audit en asynchrone pur sur un autre thread ou avoir le batching derriere ...
                // le toarray dans la réponse pose question..
                //         public static class TimeServiceInit
                // {
                //     private const int MaxJsonResponseSize = 256; // Taille maximale d'une réponse JSON
                //     private static long _currentTicks;
                //     private static readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(1));
                //     private static readonly byte[] _jsonPrefix = Encoding.UTF8.GetBytes("{\"time\":");
                //     private static readonly byte[] _jsonSuffix = Encoding.UTF8.GetBytes("}");

                //     /// <summary>
                //     /// Met à jour l'horloge de manière périodique.
                //     /// </summary>
                //     public static async Task UpdateTimeAsync()
                //     {
                //         while (await _timer.WaitForNextTickAsync())
                //         {
                //             Interlocked.Exchange(ref _currentTicks, DateTime.UtcNow.Ticks);
                //         }
                //     }

                //     /// <summary>
                //     /// Récupère les ticks actuels (pour point d'entrée basse latence).
                //     /// </summary>
                //     public static long GetCurrentTicks() => _currentTicks;

                //     /// <summary>
                //     /// Génère une réponse JSON optimisée.
                //     /// </summary>
                //     public static void BuildJsonResponse(long currentTime, Span<byte> buffer, out int length)
                //     {
                //         int offset = 0;

                //         // Préfixe JSON
                //         _jsonPrefix.CopyTo(buffer);
                //         offset += _jsonPrefix.Length;

                //         // Valeur de temps
                //         var timeStr = currentTime.ToString();
                //         offset += Encoding.UTF8.GetBytes(timeStr, buffer.Slice(offset));

                //         // Suffixe JSON
                //         _jsonSuffix.CopyTo(buffer.Slice(offset));
                //         offset += _jsonSuffix.Length;

                //         length = offset;
                //     }
                // }

                // // Audit Logger utilisant Faster
                // public class AuditLogger
                // {
                //     private readonly FasterKvStore<string, AuditEntry> _auditStore;

                //     public AuditLogger(string storePath)
                //     {
                //         _auditStore = new FasterKvStore<string, AuditEntry>(storePath, /*config options*/);
                //     }

                //     public async Task LogTimeSyncAsync(string deviceId, long timestamp, string operation)
                //     {
                //         var auditEntry = new AuditEntry
                //         {
                //             DeviceId = deviceId,
                //             Timestamp = timestamp,
                //             Operation = operation
                //         };

                //         await _auditStore.UpsertAsync(deviceId, auditEntry);
                //     }
                // }

                // // Modèle pour une entrée d'audit
                // public struct AuditEntry
                // {
                //     public string DeviceId { get; set; }
                //     public long Timestamp { get; set; }
                //     public string Operation { get; set; }
                // }

                // // Dans Program.cs
                // app.MapGet("/time-ultra/{deviceId}", async (string deviceId, AuditLogger auditLogger, HttpContext context) =>
                // {
                //     var currentTime = TimeServiceInit.GetCurrentTicks();

                //     // Log Audit (asynchronous but fire-and-forget to minimize latency)
                //     _ = auditLogger.LogTimeSyncAsync(deviceId, currentTime, "GetTime");

                //     // Préparer une réponse JSON optimisée
                //     Span<byte> responseBuffer = stackalloc byte[TimeServiceInit.MaxJsonResponseSize];
                //     TimeServiceInit.BuildJsonResponse(currentTime, responseBuffer, out var responseLength);

                //     context.Response.StatusCode = 200;
                //     context.Response.ContentType = "application/json";
                //     await context.Response.Body.WriteAsync(responseBuffer.Slice(0, responseLength).ToArray());
                // });
 
        #endregion
        int offset = 0;

        // Préfixe JSON
        _jsonPrefix.CopyTo(buffer);
        offset += _jsonPrefix.Length;

        // Valeur de temps
        var timeStr = currentTime.ToString();
        offset += Encoding.UTF8.GetBytes(timeStr, buffer.Slice(offset));

        // Suffixe JSON
        _jsonSuffix.CopyTo(buffer.Slice(offset));
        offset += _jsonSuffix.Length;

        length = offset;
    }

}


public static class TimeServiceInit2
{
    private static long _currentTicks;
    private static readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(3));
    private static readonly Channel<TaskCompletionSource<long>> _timeRequests = 
        Channel.CreateUnbounded<TaskCompletionSource<long>>(new UnboundedChannelOptions 
        { 
            SingleReader = true 
        });

    public static async Task UpdateTimeAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            Interlocked.Exchange(ref _currentTicks, DateTime.UtcNow.Ticks);
        }
    }

public static async Task ProcessTimeRequestsBatch()
{
    const int MaxBatchSize = 5000; // Augmenter la taille du batch
    while (true)
    {
        var batch = new List<TaskCompletionSource<long>>(MaxBatchSize);
        var deadline = DateTime.UtcNow.AddMicroseconds(500); // Réduit à 500µs
        
        while (DateTime.UtcNow < deadline && batch.Count < MaxBatchSize)
        {
            if (await _timeRequests.Reader.WaitToReadAsync())
            {
                while (batch.Count < MaxBatchSize && _timeRequests.Reader.TryRead(out var tcs))
                {
                    batch.Add(tcs);
                }
            }
        }

        if (batch.Count > 0)
        {
            		var time = DateTime.UtcNow.Ticks;
            		foreach (var tcs in batch)
            		{
                		tcs.TrySetResult(time);
        		    }
        		}
    		}
	}

    public static async Task<long> GetBatchedTimeAsync()
    {
        var tcs = new TaskCompletionSource<long>();
        await _timeRequests.Writer.WriteAsync(tcs);
        return await tcs.Task;
    }

    public static long GetCurrentTicks() => _currentTicks;
}

public class Program
{

	private static readonly byte[] _jsonPrefix = "{\"value\":"u8.ToArray();
	private static readonly byte[] _jsonSuffix = "}"u8.ToArray();
	private static readonly byte[] _contentTypeHeader = "application/json"u8.ToArray();


    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        /*
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            DisableDebugAndExceptionPage = true
        });
        */


            // Configuration ThreadPool
/*
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 4, Environment.ProcessorCount * 4);
            ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.SetMaxThreads(workerThreads * 2, completionPortThreads * 2);
*/
// Configuration optimale combinant les deux approches
ThreadPool.SetMinThreads(Environment.ProcessorCount * 4, Environment.ProcessorCount * 4);
ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);
ThreadPool.SetMaxThreads(
    Math.Max(workerThreads * 2, Environment.ProcessorCount * 8),
    Math.Max(completionPortThreads * 2, Environment.ProcessorCount * 8)
);



            // Services
            builder.Services.AddSingleton<IFileManager, FileManager>();
            builder.Services.AddSingleton<IAuditLogger>(_ => 
                new FasterAuditLogger(Path.Combine(AppContext.BaseDirectory, "audit.log")));

            // Configuration Kestrel
            builder.WebHost.ConfigureKestrel(options =>
            {
    options.Limits.MaxConcurrentConnections = 100_000;
    options.Limits.MaxConcurrentUpgradedConnections = 100_000;
    options.Limits.MaxRequestBodySize = 1024;
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MinResponseDataRate = null;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMilliseconds(100);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(10);

        /*
            options.ListenAnyIP(5000, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                listenOptions.NoDelay = true;  // Désactive l'algorithme de Nagle
            });
        */

                /*options.Listen(IPAddress.Any, 5000, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    // Configuration des options de socket directement
                    if (listenOptions.ConnectionAdapters is SocketConnectionListener socketListener)
                    {
                            socketListener.Socket.NoDelay = true;  // Désactive Nagle's algorithm
                            socketListener.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    }
                });*/


        // HTTP1 et HTTP2 sur le port 5000
        options.Listen(IPAddress.Any, 5000, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;  // HTTP1 seulement sans TLS



        });

        // HTTPS avec HTTP2 sur le port 5001
        options.Listen(IPAddress.Any, 5001, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            listenOptions.UseHttps();
            
            // Configuration bas niveau du socket
            listenOptions.UseConnectionLogging();
           // listenOptions.NoDelay = true;

                
        });





            });


        // Désactiver les features non nécessaires
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.AddServerHeader = false;
        });
        // Eviter le warning de conflit avec la configuration par défaut
        builder.WebHost.UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001");


        // Désactiver les logs non essentiels
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        


            var app = builder.Build();


            // Démarrer le service en arrière-plan
            var auditLogger = app.Services.GetRequiredService<IAuditLogger>();
            _ = TimeServiceInit.UpdateTimeAsync();
            _ = TimeServiceInit.ProcessTimeRequestsBatch(auditLogger);


            //Midleware 404 warning distingo
            app.Use(async (context, next) =>
            {
                await next();

                if (context.Response.StatusCode == 404)
                {
                    var endpoint = context.GetEndpoint();
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

                    if (endpoint == null)
                    {
                        logger.LogWarning("Invalid path accessed: {Path}", context.Request.Path);
                    }
                }
            });

            // Endpoints
            app.MapGet("/manifest/{deviceId}", async (string deviceId, IAuditLogger audit) =>
            {
                await audit.LogManifestRequestAsync(deviceId);
                return Results.Ok(new[] { new ManifestResource() });
            });

            app.MapGet("/download/{fileId}", async (string fileId, 
                [AsParameters] ChunkRequest request,
                IFileManager files,
                IAuditLogger audit) =>
            {
                if (!request.IsValid)
                    return Results.BadRequest();

                await audit.LogDownloadAsync(fileId, request.FileId, request.Offset, request.Size);
                var chunk = await files.GetChunkAsync(fileId, request.Offset, request.Size);
                return Results.Ok(chunk.ToArray());
            });

            app.MapPost("/confirm/{deviceId}/{fileId}", async (string deviceId, 
                string fileId,
                IAuditLogger audit) =>
            {
                await audit.LogConfirmationAsync(deviceId, fileId);
                return Results.Ok();
            });

        // app.MapGet("/time", () => Results.Ok(TimeServiceInit.GetCurrentTicks()));

    app.MapGet("/time/{deviceId}", async (string deviceId, IAuditLogger audit, HttpContext context) =>
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";

        await audit.LogTimeSyncAsync(deviceId);

        
        await context.Response.Body.WriteAsync(_jsonPrefix);
        var timeBytes = Encoding.UTF8.GetBytes(TimeServiceInit.GetCurrentTicks().ToString());
        await context.Response.Body.WriteAsync(timeBytes);
        await context.Response.Body.WriteAsync(_jsonSuffix);
    });



        //app.MapGet("/time-batched", async () => Results.Ok(await TimeServiceInit.GetBatchedTimeAsync()));

        app.MapGet("/time-batched/{deviceId}", async (string deviceId) =>
        {
            var response = await TimeServiceInit.GetBatchedTimeAsync(deviceId);
            return Results.Bytes(response, "application/json");
        });


            await app.RunAsync();
    
    }


/* Alternative used for testing with ZMQ */
public static async Task MainZMQ(string[] args)
{
    Console.WriteLine("Starting IoTHighPerf server...");

    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            Console.WriteLine("Configuring services...");

            // ThreadPool optimisations
             ThreadPool.SetMinThreads(Environment.ProcessorCount * 4, Environment.ProcessorCount * 4);
            ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.SetMaxThreads(workerThreads * 2, completionPortThreads * 2);


            services.AddSingleton<IFileManager, FileManager>();
            services.AddSingleton<IAuditLogger>(_ => 
                new FasterAuditLogger(Path.Combine(AppContext.BaseDirectory, "audit.log")));

        // Audit logger avec channel dédié
            // var auditLogger = new FasterAuditLogger(
            //     Path.Combine(AppContext.BaseDirectory, "audit.log"), 
            //     new Channel<AuditEvent>(100_000, channelOptions));
            // services.AddSingleton<IAuditLogger>(auditLogger);




            services.Configure<ZeroMQConfig>(config => {
                config.TimeEndpoint = "tcp://*:5555";


            });
            services.AddSingleton<ITimeService, TimeService>();  
            services.AddHostedService<TimeService>();
            services.AddHostedService<TimeServer>();
        })
        .Build();

    Console.WriteLine("Starting host...");
    await host.RunAsync();
}


}