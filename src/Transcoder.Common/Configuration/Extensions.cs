using Transcoder.Common.Configuration;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class Extensions
{
    public static IServiceCollection ConfigureMessageQueues(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<QueueOptions>(QueueOptions.VideoQueue, config.GetSection($"YandexCloud:{QueueOptions.VideoQueue}"));
        services.Configure<QueueOptions>(QueueOptions.StatusQueue, config.GetSection($"YandexCloud:{QueueOptions.StatusQueue}"));
        services.Configure<QueueOptions>(QueueOptions.ChunkQueue, config.GetSection($"YandexCloud:{QueueOptions.ChunkQueue}"));
        services.Configure<QueueOptions>(QueueOptions.StreamQueue, config.GetSection($"YandexCloud:{QueueOptions.StreamQueue}"));
        services.Configure<QueueOptions>(QueueOptions.ProcessedChunksQueue, config.GetSection($"YandexCloud:{QueueOptions.ProcessedChunksQueue}"));
        
        return services;
    }

    public static IServiceCollection ConfigureSharedStorage(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SharedStorageOptions>(config.GetSection("YandexCloud:SharedStorage"));

        return services;
    }

    public static IServiceCollection ConfigureBuckets(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BucketOptions>(BucketOptions.ContentBucket, configuration.GetSection($"YandexCloud:{BucketOptions.ContentBucket}"));
        services.Configure<BucketOptions>(BucketOptions.FilesBucket, configuration.GetSection($"YandexCloud:{BucketOptions.FilesBucket}"));
        services.Configure<BucketOptions>(BucketOptions.TranscoderBucket, configuration.GetSection($"YandexCloud:{BucketOptions.TranscoderBucket}"));

        return services;
    }
}
