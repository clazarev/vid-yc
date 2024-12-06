using Amazon.S3;
using Amazon.SQS;
using Transcoder.Chunker;
using Transcoder.Chunker.Interfaces;
using Transcoder.Chunker.Models;
using Transcoder.Common;
using Transcoder.Common.Configuration;
using Transcoder.Common.Health;
using Transcoder.Common.Storage;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

//mounted as a pre-created ConfigMap in k8s
builder.Configuration.AddAppsettingsCommon();

builder.Services.AddSerilog(configuration =>
{
    configuration.ReadFrom
        .Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName();
});

builder.Services
    .ConfigureSharedStorage(builder.Configuration)
    .ConfigureMessageQueues(builder.Configuration)
    .ConfigureBuckets(builder.Configuration)
    .Configure<ProcessingOptions>(builder.Configuration.GetSection("ProcessingOptions"));

builder.Services
    .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
    .AddAWSService<IAmazonSQS>()
    .AddAWSService<IAmazonS3>(builder.Configuration.GetAWSOptions("S3"))
    .AddSingleton<StatusSender>()
    .AddSingleton<IFileStorageService, FileStorageService>()
    ;

builder.Services
    .Configure<HostOptions>(options =>
    {
        options.ServicesStartConcurrently = true;
        options.ServicesStopConcurrently = true;
        options.ShutdownTimeout = TimeSpan.FromMinutes(10);
    })
    .AddHostedService<Worker>()
#if !SKIP_AUDIO
    .AddHostedService<AudioWorker>()
    .AddHostedService<AudioWorker2>()
#endif
    .AddSingleton<IBackgroundTaskQueue<AudioWorkItem>>(_ => new AudioBackgroundTaskQueue(10))
    .AddHostedService<WorkerHealthCheckBackgroundService>();

var host = builder.Build();
host.Run();
