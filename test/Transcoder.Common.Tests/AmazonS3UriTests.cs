using Amazon.S3.Util;

namespace Transcoder.Common.Tests;

public class AmazonS3UriTests
{
    [Fact]
    public void SignedUrlDownload()
    {
        var url = "https://s3.yandexcloud.net/private_bucket/ed023f8d-1866-4720-b6a7-a89fc9a5b6b6/files/88d6483f-b5ab-44dd-b052-378ba0b0c165.mp4?X-Amz-Expires=604800&X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=YCAJESfTM7dQCT7oYptL5SA8F%2F20241125%2Fru-central1%2Fs3%2Faws4_request&X-Amz-Date=20241125T135954Z&X-Amz-SignedHeaders=host&X-Amz-Signature=ef570652cf88f42084416ab21d9b939a01a88e7705c099029b717da292ffae15";

        AmazonS3Uri uri = new(url);
        string expected = "private_bucket";
        string expectedKey = "ed023f8d-1866-4720-b6a7-a89fc9a5b6b6/files/88d6483f-b5ab-44dd-b052-378ba0b0c165.mp4";

        Assert.Equal(expected, uri.Bucket);
        Assert.Equal(expectedKey, uri.Key);
    }
}
