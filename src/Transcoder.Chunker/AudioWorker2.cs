using Transcoder.Common.Configuration;
using Transcoder.Common.Storage;

using Microsoft.Extensions.Options;
using Transcoder.Chunker.Interfaces;
using Transcoder.Chunker.Models;

namespace Transcoder.Chunker;

public class AudioWorker2(
    IHostApplicationLifetime applicationLifetime,
    IBackgroundTaskQueue<AudioWorkItem> taskQueue,
    Serilog.ILogger logger,
    FileStorageService fileService,
    IOptions<SharedStorageOptions> storageOptions
) : AudioWorker(applicationLifetime, taskQueue, logger, fileService, storageOptions);
