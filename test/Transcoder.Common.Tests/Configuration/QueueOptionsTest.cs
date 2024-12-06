using JetBrains.Annotations;
using Transcoder.Common.Configuration;

namespace Transcoder.Common.Tests.Configuration;

[TestSubject(typeof(QueueOptions))]
public class QueueOptionsTest
{
    [Fact]
    public void Properties_NameUrl()
    {
        var options = new QueueOptions
        {
            Url = "Url"
        };

        Assert.Equal("Url", options.Url);
    }
}
