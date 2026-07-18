namespace AtomicArt.Desktop.Services.Generation.State;

internal static class PanelAttachmentStateSanitizer
{
    public static bool IsValid(PanelAttachmentState? attachment)
    {
        return attachment is not null
            && !string.IsNullOrWhiteSpace(attachment.Id)
            && !string.IsNullOrWhiteSpace(attachment.FileName)
            && !string.IsNullOrWhiteSpace(attachment.ContentType)
            && !string.IsNullOrWhiteSpace(attachment.InternalFileName)
            && attachment.SizeBytes >= 0;
    }

    public static IReadOnlyList<PanelAttachmentState> Sanitize(
        IReadOnlyList<PanelAttachmentState>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return [];
        }

        return attachments
            .Where(IsValid)
            .Select(attachment => new PanelAttachmentState
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.SizeBytes,
                InternalFileName = attachment.InternalFileName
            })
            .ToList();
    }
}
