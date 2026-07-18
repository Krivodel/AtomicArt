using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace Pica.Viewer;

public static class DependencyInjection
{
    public static IServiceCollection AddPicaViewer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IImageFormatRegistry, ImageFormatRegistry>();
        services.AddSingleton<IImageViewerStateService, ImageViewerStateService>();
        services.AddSingleton<ImagePreviewLoader>();
        services.AddSingleton<FullResolutionImageLoader>();
        services.AddSingleton<PngImageEncoder>();
        services.AddSingleton<ClipboardImagePreparer>();
        services.AddSingleton<IPlatformFileActions>(provider =>
            PlatformFileActionsFactory.Create(
                provider.GetRequiredService<ILogger<WindowsApplicationIconLoader>>()));
        services.AddSingleton<AvaloniaClipboardDataWriter>();
        services.AddSingleton<IPlatformClipboardImageWriter>(provider =>
            PlatformClipboardImageWriterFactory.Create(
                provider.GetRequiredService<AvaloniaClipboardDataWriter>(),
                provider.GetRequiredService<ClipboardImagePreparer>()));
        services.AddSingleton<ClipboardImageWriter>(provider =>
            new ClipboardImageWriter(
                provider.GetRequiredService<AvaloniaClipboardDataWriter>(),
                provider.GetRequiredService<IPlatformClipboardImageWriter>()));
        services.AddSingleton<IClipboardImageWriter>(provider =>
            provider.GetRequiredService<ClipboardImageWriter>());
        services.AddSingleton<IViewerClipboardWriter>(provider =>
            provider.GetRequiredService<ClipboardImageWriter>());
        services.AddSingleton<IImageViewerWindowFactory>(provider =>
            new ImageViewerWindowFactory(
                provider.GetRequiredService<IViewerClipboardWriter>(),
                provider.GetRequiredService<IImageFormatRegistry>(),
                provider.GetRequiredService<IImageViewerStateService>(),
                provider.GetRequiredService<ImagePreviewLoader>(),
                provider.GetRequiredService<FullResolutionImageLoader>(),
                provider.GetRequiredService<PngImageEncoder>(),
                provider.GetRequiredService<ClipboardImagePreparer>(),
                provider.GetRequiredService<IPlatformFileActions>(),
                provider.GetRequiredService<ILogger<ImageViewerWindow>>(),
                provider.GetRequiredService<ILogger<TemporarySelectionFileStore>>()));

        return services;
    }
}
