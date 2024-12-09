using AutoMapper;
using Transcoder.Common.Entities;
using Transcoder.Common.MessageModels;

namespace Transcoder.ChunkSaver.Mapping;

// ReSharper disable once UnusedType.Global
#pragma warning disable CA1812
internal sealed class ChunksModelsMapping : Profile
{
    public ChunksModelsMapping()
    {
        CreateMap<ProcessedChunkMessage, ProcessedChunk>();
    }
}
#pragma warning restore CA1812
