namespace Transcoder.Common.Configuration;

public class MongoOptions
{
    public string? MongoConnectionString { get; set; }
    public string? MongoDatabaseName { get; set; }

    public int PingTimeout { get; set; } = 3000;
}
