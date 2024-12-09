using System.Threading.Channels;
using Transcoder.Chunker.Interfaces;
using Transcoder.Chunker.Models;

namespace Transcoder.Chunker;

internal sealed class AudioBackgroundTaskQueue: IBackgroundTaskQueue<AudioWorkItem>
{
    private readonly Channel<AudioWorkItem> _queue;

    public AudioBackgroundTaskQueue(int capacity)
    {
        BoundedChannelOptions options = new(capacity)
        {
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<AudioWorkItem>(options);
    }

    public async ValueTask ProduceAsync(
        AudioWorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<AudioWorkItem> ConsumeAsync(
        CancellationToken cancellationToken)
    {
        var workItem =
            await _queue.Reader.ReadAsync(cancellationToken);

        return workItem;
    }
}
