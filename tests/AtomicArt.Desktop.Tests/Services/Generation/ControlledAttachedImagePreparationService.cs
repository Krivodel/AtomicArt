using System.Collections.Concurrent;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class ControlledAttachedImagePreparationService :
    IAttachedImagePreparationService
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _started = new(
        StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AttachedImageDto?>> _completions = new(
        StringComparer.Ordinal);

    public ControlledAttachedImagePreparationService(IEnumerable<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        foreach (string fileName in fileNames)
        {
            _started[fileName] = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _completions[fileName] = new TaskCompletionSource<AttachedImageDto?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public async Task<AttachedImageDto?> PrepareAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(selectedModel);

        TaskCompletionSource started = GetStarted(image.FileName);
        TaskCompletionSource<AttachedImageDto?> completion = GetCompletion(image.FileName);
        started.TrySetResult();

        return await completion.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    public async Task WaitUntilStartedAsync(string fileName)
    {
        await GetStarted(fileName).Task.ConfigureAwait(false);
    }

    public void Complete(AttachedImageDto image)
    {
        ArgumentNullException.ThrowIfNull(image);

        GetCompletion(image.FileName).TrySetResult(image);
    }

    private TaskCompletionSource GetStarted(string fileName)
    {
        return _started.TryGetValue(fileName, out TaskCompletionSource? started)
            ? started
            : throw new InvalidOperationException($"No controlled preparation exists for '{fileName}'.");
    }

    private TaskCompletionSource<AttachedImageDto?> GetCompletion(string fileName)
    {
        return _completions.TryGetValue(
            fileName,
            out TaskCompletionSource<AttachedImageDto?>? completion)
            ? completion
            : throw new InvalidOperationException($"No controlled preparation exists for '{fileName}'.");
    }
}
