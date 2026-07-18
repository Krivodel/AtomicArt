using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AtomicArt.Desktop.Views;

public sealed class ViewModelViewTemplate : IDataTemplate
{
    private readonly IReadOnlyList<ViewTemplateRegistration> _registrations;

    public ViewModelViewTemplate(IReadOnlyList<ViewTemplateRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        _registrations = registrations;
    }

    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        ViewTemplateRegistration registration = GetRegistration(param);
        Control view = registration.CreateView();
        view.DataContext = param;

        return view;
    }

    public bool Match(object? data)
    {
        return data is not null && _registrations.Any(registration =>
            registration.ViewModelType.IsInstanceOfType(data));
    }

    private ViewTemplateRegistration GetRegistration(object data)
    {
        return _registrations.First(registration =>
            registration.ViewModelType.IsInstanceOfType(data));
    }
}
