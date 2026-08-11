using Avalonia.Input;

namespace AtomicArt.Desktop.Services;

internal static class AtomicArtPromptDragData
{
    private const string PromptFormatIdentifier = "AtomicArt.GenerationPrompt";

    private static readonly DataFormat<string> PromptFormat =
        DataFormat.CreateInProcessFormat<string>(PromptFormatIdentifier);

    public static DataTransfer Create(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        DataTransferItem item = DataTransferItem.CreateText(prompt);
        item.Set(PromptFormat, prompt);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(item);

        return dataTransfer;
    }

    public static bool IsPrompt(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        return dataTransfer.Contains(PromptFormat);
    }

    public static string? GetPromptOrDefault(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        foreach (IDataTransferItem item in dataTransfer.Items)
        {
            if (item.TryGetRaw(PromptFormat) is string prompt)
            {
                return prompt;
            }
        }

        return null;
    }
}
