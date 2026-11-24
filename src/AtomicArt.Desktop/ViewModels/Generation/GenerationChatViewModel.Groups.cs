using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatViewModel
{
    [RelayCommand(CanExecute = nameof(CanReturn))]
    private Task ReturnToSourceAsync(CancellationToken ct)
    {
        GenerationItemViewModel? source = _gallery?.Items.FirstOrDefault(card => card.Id == _active?.Group?.SourceCardId);

        return source is null ? Task.CompletedTask : OpenAsync(source, ct);
    }

    [RelayCommand(CanExecute = nameof(CanOpenGroup))]
    private Task OpenGroupAsync(GenerationChatGroupViewModel? link, CancellationToken ct)
    {
        return link is null ? Task.CompletedTask
            : ExecuteAsync(token => OpenGroupSessionAsync(link.Group, token), nameof(OpenGroupAsync), ct);
    }

    private bool CanReturn()
    {
        return !IsLoading && CanReturnToSource;
    }

    private bool CanOpenGroup(GenerationChatGroupViewModel? link)
    {
        return !_isDisposed && !IsLoading && link is not null;
    }

    private async Task OpenGroupSessionAsync(GenerationChatGroup group, CancellationToken ct)
    {
        long version = ++_openVersion;

        if (_gallery is null || _panel is null)
        {
            return;
        }

        if (!_groups.TryGetValue(group.Id, out GenerationChatSessionViewModel? session))
        {
            GenerationItemViewModel? representative = _gallery.Items.FirstOrDefault(card => card.ChatGroup?.Id == group.Id);

            if (representative is null)
            {
                return;
            }

            session = _createSession();
            session.Connect(_gallery, _panel);

            try
            {
                await session.OpenGroupAsync(group, representative, ct);

                if (_isDisposed || version != _openVersion || !session.HasGroupVariants)
                {
                    session.Dispose();
                    return;
                }

                session.PropertyChanged += OnGroupChanged;
                _groups.Add(group.Id, session);
            }
            catch (Exception)
            {
                session.Dispose();
                throw;
            }
        }

        GenerationItemViewModel? source = _gallery.Items.FirstOrDefault(card => card.ChatGroup?.Id == group.Id) ?? session.Source;

        if (source is null)
        {
            return;
        }

        GenerationPanelPreset preset = new(source.ModelId, string.Empty, source.AspectRatio, source.Resolution,
            source.RequestSettings, References: null);

        if (await _panel.TryApplyPresetAsync(preset, ct) && !_isDisposed && version == _openVersion && session.HasGroupVariants)
        {
            Activate(session);
            IsExpanded = true;
        }
    }

    private void RefreshGroupLinks()
    {
        if (_active is null || _gallery is null || _active.Messages.Count == 0 || _active.IsGroup
            || _active.Source is not { } source)
        {
            return;
        }

        Dictionary<Guid, GenerationChatGroupViewModel> links = [];

        foreach (IGrouping<Guid, GenerationItemViewModel> cards in _gallery.Items.Where(card => card.ChatGroup?.SourceCardId == source.Id)
            .GroupBy(card => card.ChatGroup?.Id ?? Guid.Empty))
        {
            GenerationItemViewModel first = cards.OrderBy(card => card.CreatedAtUtc).First();

            if (first.ChatGroup is { } group)
            {
                links.Add(group.Id, new GenerationChatGroupViewModel(group, first.Prompt, cards.Count(), _formatter));
            }
        }

        foreach (GenerationChatSessionViewModel session in _groups.Values.Where(session => session.Group?.SourceCardId == source.Id))
        {
            if (session.Group is { } group && session.HasGroupVariants)
            {
                links[group.Id] = new GenerationChatGroupViewModel(group, session.RepeatPrompt ?? string.Empty,
                    session.Messages[^1].Images.Count, _formatter);
            }
            else if (session.Group is { } empty)
            {
                links.Remove(empty.Id);
            }
        }

        _active.Messages[^1].SetGroups(links.Values.OrderBy(link => link.Group.CreatedAtUtc)
            .ThenBy(link => link.Group.Id).ToArray());
    }

    private void OnGroupChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationChatSessionViewModel.HasGroupVariants))
        {
            RefreshGroupLinks();
        }
    }

    private void OnGroupLabelsChanged(object? sender, EventArgs e)
    {
        RefreshGroupLinks();
    }
}
