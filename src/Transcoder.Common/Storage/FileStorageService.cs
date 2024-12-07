using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;
using System.Net;
using Transcoder.Common.Configuration;

namespace Transcoder.Common.Storage;

public interface IFileStorageService
{
    Task UploadFileToContentBucket(string path, string key, CancellationToken token);
    Task UploadFileToTranscoderBucket(string path, string key, CancellationToken token);
    Task UploadDirectoryToContentBucket(string path, EventHandler<UploadDirectoryProgressArgs> onProgress, CancellationToken token);
    Task UploadDirectoryToTranscoderBucket(string path, EventHandler<UploadDirectoryProgressArgs> onProgress, CancellationToken token);

    Task DownloadFile(string path, string key, CancellationToken token);
}

public class FileStorageService(
    IAmazonS3 client,
    IOptionsMonitor<BucketOptions> bucketOptions) : IFileStorageService
{
    private readonly string _contentBucket = bucketOptions.Get(BucketOptions.ContentBucket).Name!;
    private readonly string _transcoderBucket = bucketOptions.Get(BucketOptions.TranscoderBucket).Name!;

    public Task UploadFileToContentBucket(string path, string key, CancellationToken token)
    {
        return UploadFile(path, key, _contentBucket, token);
    }

    public Task UploadFileToTranscoderBucket(string path, string key, CancellationToken token)
    {
        return UploadFile(path, key, _transcoderBucket, token);
    }

    public async Task DownloadFile(string path, string key, CancellationToken token)
    {
        GetObjectRequest objectRequest = new()
        {
            BucketName = _transcoderBucket,
            Key = key
        };

        using var response = await client.GetObjectAsync(objectRequest, token);
        if (response.HttpStatusCode == HttpStatusCode.OK)
        {
            await using var fs = new FileStream(path, FileMode.OpenOrCreate);
            await response.ResponseStream.CopyToAsync(fs, token);
        }
    }

    public Task UploadDirectoryToContentBucket(string path, EventHandler<UploadDirectoryProgressArgs> onProgress, CancellationToken token)
    {
        return UploadDirectoryToBucket(path, _contentBucket, onProgress, token);
    }

    public Task UploadDirectoryToTranscoderBucket(string path, EventHandler<UploadDirectoryProgressArgs> onProgress,
        CancellationToken token)
    {
        return UploadDirectoryToBucket(path, _transcoderBucket, onProgress, token);
    }

    private async Task UploadFile(string path, string key, string bucket, CancellationToken token)
    {
        PutObjectRequest putObjectRequest = new()
        {
            BucketName = bucket,
            Key = key,
            FilePath = path
        };

        await client.PutObjectAsync(putObjectRequest, token);
    }

    private async Task UploadDirectoryToBucket(string path, string bucket, EventHandler<UploadDirectoryProgressArgs> onProgress, CancellationToken token)
    {
#pragma warning disable CA2000
        var directoryTransferUtility = new TransferUtility(client);
#pragma warning restore CA2000
        var request = new TransferUtilityUploadDirectoryRequest
        {
            BucketName = bucket,
            Directory = path,
            UploadFilesConcurrently = true,
            SearchPattern = "*",
            SearchOption = SearchOption.AllDirectories,
        };
        request.UploadDirectoryProgressEvent += onProgress;

        await directoryTransferUtility
            .UploadDirectoryAsync(request, token);
    }
}
