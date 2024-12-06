using Amazon.S3;
using Amazon.SQS;
using Transcoder.Common;
using Transcoder.Common.Configuration;
using Transcoder.Common.Health;
using Transcoder.Common.Storage;
using Transcoder.Composer;
using Transcoder.Composer.Application.Interfaces;
using Transcoder.Composer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

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
    .Configure<MongoOptions>(builder.Configuration.GetSection(nameof(MongoOptions)))
    .ConfigureSharedStorage(builder.Configuration)
    .ConfigureMessageQueues(builder.Configuration)
    .ConfigureBuckets(builder.Configuration)
    ;

builder.Services
    .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
    .AddAWSService<IAmazonSQS>();

builder.Services
    .AddDbContext<ApplicationDbContext>(optionsBuilder =>
    {
        var databaseName = new MongoUrl(builder.Configuration.GetConnectionString("MongoDb")).DatabaseName;
        optionsBuilder.UseMongoDB(builder.Configuration.GetConnectionString("MongoDb")!, databaseName);
    })
    .AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>())
    .AddScoped<IChunkRepository, ChunkRepository>()
    .AddScoped<IVideoProfilesRepository, VideoProfilesRepository>()
    .AddAWSService<IAmazonS3>(builder.Configuration.GetAWSOptions("S3"))
    .AddSingleton<IFileStorageService, FileStorageService>()
    .AddSingleton<StatusSender>();

builder.Services
    .Configure<HostOptions>(options =>
    {
        options.ServicesStartConcurrently = true;
        options.ServicesStopConcurrently = true;
        options.ShutdownTimeout = TimeSpan.FromMinutes(10);
    })
    .AddHostedService<Worker>()
    .AddHostedService<WorkerHealthCheckBackgroundService>();

var host = builder.Build();

host.Run();
