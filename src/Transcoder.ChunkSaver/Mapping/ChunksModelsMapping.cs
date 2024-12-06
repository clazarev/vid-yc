using AutoMapper;
using Transcoder.Common.Entities;
using Transcoder.Common.MessageModels;

namespace Transcoder.ChunkSaver.Mapping;

public class ChunksModelsMapping : Profile
{
    public ChunksModelsMapping()
    {
        CreateMap<ProcessedChunkMessage, ProcessedChunk>();
    }
}
