using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingConfirmationDialogPresenter :
    IConfirmationDialogPresenter
{
    public bool IsOpen { get; private set; }
    public bool Result { get; set; }
    public IReadOnlyList<ConfirmationDialogPresentation> Presentations =>
        _presentations;
    public IReadOnlyList<ConfirmationDialogPresentation> Updates => _updates;
    public bool WaitForCompletion { get; set; }

    private readonly List<ConfirmationDialogPresentation> _presentations = [];
    private readonly List<ConfirmationDialogPresentation> _updates = [];
    private TaskCompletionSource<bool>? _completion;

    public async Task<bool> ShowAsync(
        ConfirmationDialogPresentation presentation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ct.ThrowIfCancellationRequested();
        _presentations.Add(presentation);
        IsOpen = true;

        try
        {
            if (!WaitForCompletion)
            {
                return Result;
            }

            _completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            return await _completion.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _completion = null;
            IsOpen = false;
        }
    }

    public void Update(ConfirmationDialogPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _updates.Add(presentation);
    }

    public void Dismiss()
    {
        _completion?.TrySetResult(false);
    }

    public void Complete(bool result)
    {
        _completion?.TrySetResult(result);
    }
}
