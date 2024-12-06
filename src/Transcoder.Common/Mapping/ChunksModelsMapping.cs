using AutoMapper;
using Transcoder.Common.Entities;
using Transcoder.Common.MessageModels;

namespace Transcoder.Common.Mapping;

public class ChunksModelsMapping : Profile
{
    public ChunksModelsMapping()
    {
        CreateMap<ProcessedChunkMessage, ProcessedChunk>();
    }
}
