using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Transcoder.Common.Entities;

public record ProcessedChunk
{
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("createDate")]
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    [BsonElement("total")]
    public int Total { get; set; }

    [BsonElement("chunkNumber")]
    public int ChunkNumber { get; set; }

    [BsonElement("chunkName")]
    public string? ChunkName { get; set; }

    [BsonElement("videoId")]
    [BsonRepresentation(BsonType.String)]
    public Guid VideoId { get; set; }

    [BsonElement("key")]
    public string? Key { get; set; }

    [BsonElement("audioKey")]
    public string? AudioKey { get; set; }

    [BsonElement("height")]
    public int Height { get; set; }
}
