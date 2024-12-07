using Amazon.SQS;
using Transcoder.ChunkSaver;
using Transcoder.ChunkSaver.Application.Interfaces;
using Transcoder.ChunkSaver.Infrastructure;
using Transcoder.Common;
using Transcoder.Common.Configuration;
using Transcoder.Common.Health;
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
    .ConfigureMessageQueues(builder.Configuration);

builder.Services
    .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
    .AddAWSService<IAmazonSQS>();

builder.Services
    .AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services
    .AddDbContext<ApplicationDbContext>(optionsBuilder =>
    {
        var databaseName = new MongoUrl(builder.Configuration.GetConnectionString("MongoDb")).DatabaseName;
        optionsBuilder.UseMongoDB(builder.Configuration.GetConnectionString("MongoDb")!, databaseName);
    })
    .AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>())
    .AddScoped<IChunkRepository, ChunkRepository>()
    .AddSingleton<StatusSender>();

builder.Services
    .Configure<HostOptions>(options =>
    {
        options.ServicesStartConcurrently = true;
        options.ServicesStopConcurrently = true;
        options.ShutdownTimeout = TimeSpan.FromSeconds(60);
    })
    .AddHostedService<Worker>()
    .AddHostedService<WorkerHealthCheckBackgroundService>();

var host = builder.Build();

#pragma warning disable S6966
host.Run();
#pragma warning restore S6966
