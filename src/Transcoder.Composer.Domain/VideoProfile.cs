using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Transcoder.Composer.Domain;
public record VideoProfile
{
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("videoId")]
    [BsonRepresentation(BsonType.String)]
    public Guid VideoId { get; set; }

    [BsonElement("width")]
    public int Width { get; set; }

    [BsonElement("height")]
    public int Height { get; set; }

    [BsonElement("bitRate")]
    public long BitRate { get; set; }

    [BsonElement("codec")]
    public string? Codec { get; set; }
}
