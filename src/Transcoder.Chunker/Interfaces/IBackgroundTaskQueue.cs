namespace Transcoder.Chunker.Interfaces;

internal interface IBackgroundTaskQueue<T> where T : IWorkItem
{
    ValueTask ProduceAsync(
        T workItem);

    ValueTask<T> ConsumeAsync(
        CancellationToken cancellationToken);
}
