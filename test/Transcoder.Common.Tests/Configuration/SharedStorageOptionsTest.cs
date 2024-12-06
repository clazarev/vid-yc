using JetBrains.Annotations;
using Transcoder.Common.Configuration;

namespace Transcoder.Common.Tests.Configuration;

[TestSubject(typeof(SharedStorageOptions))]
public class SharedStorageOptionsTest
{
    [Fact]
    public void Path_Property()
    {
        var options = new SharedStorageOptions
        {
            Path = "test"
        };

        Assert.Equal("test", options.Path);
    }
}
