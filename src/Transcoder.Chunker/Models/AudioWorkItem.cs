using Transcoder.Chunker.Interfaces;

namespace Transcoder.Chunker.Models;

public record AudioWorkItem(Guid VideoId, string OriginalVideoFilePath, string AudioName, string UploadObjectKey) : IWorkItem;
