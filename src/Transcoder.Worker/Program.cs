using Transcoder.Common.Health;
using Amazon.S3;
using Transcoder.Common.Storage;
using Transcoder.Worker;

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
builder.Services.Configure<HostOptions>(
    opts => opts.ShutdownTimeout = TimeSpan.FromMinutes(1));

builder.Services
    .ConfigureSharedStorage(builder.Configuration)
    .ConfigureMessageQueues(builder.Configuration)
    .ConfigureBuckets(builder.Configuration)
    ;

builder.Services
    .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
    .AddAWSService<IAmazonSQS>()
    .AddAWSService<IAmazonS3>(builder.Configuration.GetAWSOptions("S3"))
    .AddSingleton<IFileStorageService, FileStorageService>()
    ;

builder.Services
    .Configure<HostOptions>(options =>
    {
        options.ServicesStartConcurrently = true;
        options.ServicesStopConcurrently = true;
        options.ShutdownTimeout = TimeSpan.FromSeconds(90);
    })
    .AddHostedService<Worker>()
    .AddHostedService<WorkerHealthCheckBackgroundService>();

var host = builder.Build();

#pragma warning disable S6966
host.Run();
#pragma warning restore S6966
