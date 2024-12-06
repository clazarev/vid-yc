using Amazon.S3;
using Amazon.SQS;
using Transcoder.API;
using Transcoder.API.Application.Interfaces;
using Transcoder.API.HealthChecks;
using Transcoder.API.Infrastructure;
using Transcoder.Common.Health;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
    ;

builder.Services
    .AddAWSService<IAmazonSQS>(builder.Configuration.GetAWSOptions("SQS"))
    .AddAWSService<IAmazonS3>(builder.Configuration.GetAWSOptions("S3"))
    ;
builder.Services
    .AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies())
    .AddGrpc();

builder.Services
    .AddHealthChecks();

builder.Services
    .AddDbContext<ApplicationDbContext>(optionsBuilder =>
    {
        var databaseName = new MongoUrl(builder.Configuration.GetConnectionString("MongoDb")).DatabaseName;
        optionsBuilder.UseMongoDB(builder.Configuration.GetConnectionString("MongoDb")!, databaseName);
    })
    .AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>())
    .AddScoped<IVideoService, Transcoder.API.Application.VideoService>();

builder.Services.AddGrpcHealthChecks().AddCheck<DatabaseHealthCheck>("database");

builder.Services
    .Configure<HostOptions>(options =>
    {
        options.ServicesStartConcurrently = true;
        options.ServicesStopConcurrently = true;
    })
    .AddHostedService<Worker>()
    .AddHostedService<WorkerHealthCheckBackgroundService>();

var app = builder.Build();

app.MapGrpcService<VideoService>();
app.MapGrpcHealthChecksService();

app.Run();
