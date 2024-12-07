using AutoMapper;
using Transcoder.Common.Entities;
using Transcoder.Common.MessageModels;

namespace Transcoder.ChunkSaver.Mapping;

// ReSharper disable once UnusedType.Global
internal class ChunksModelsMapping : Profile
{
    public ChunksModelsMapping()
    {
        CreateMap<ProcessedChunkMessage, ProcessedChunk>();
    }
}
