using Transcoder.Chunker.Interfaces;

namespace Transcoder.Chunker.Models;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed record AudioWorkItem(Guid VideoId, string OriginalVideoFilePath, string AudioName, string UploadObjectKey) : IWorkItem;
