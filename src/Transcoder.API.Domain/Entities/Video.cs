using Transcoder.Common.MessageModels;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Transcoder.API.Domain.Entities;

public record Video
{
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("createDate")]
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    [BsonElement("updateDate")]
    public DateTime UpdateDate { get; set; } = DateTime.UtcNow;

    [BsonElement("playlist")]
    public string Playlist { get; set; } = string.Empty;

    [BsonElement("fileUrl")]
    public string FileUrl { get; set; } = string.Empty;

    [BsonElement("format")]
    public string Format { get; set; } = string.Empty;

    [BsonElement("codec")]
    public string Codec { get; set; } = string.Empty;

    [BsonElement("duration")]
    public long Duration { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public VideoStatus Status { get; set; }

    [BsonElement("progress")]
    public int Progress { get; set; }

    [BsonElement("resolutionProgress")]
    public List<ResolutionProgress> ResolutionProgress { get; set; } = [];
}

public record ResolutionProgress()
{
    public int Height { get; set; }
    public int Progress { get; set; }
}
