namespace Transcoder.Common.Configuration;

public class BucketOptions
{
    public const string FilesBucket = nameof(FilesBucket);
    public const string ContentBucket = nameof(ContentBucket);
    public const string TranscoderBucket = nameof(TranscoderBucket);

    public string? Name { get; set; }
}
