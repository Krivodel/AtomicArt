using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pica.Viewer.Services;

namespace Pica.Viewer;

public static class DependencyInjection
{
    public static IServiceCollection AddPicaViewer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ImageFormatRegistry>();
        services.AddSingleton<IImageFormatRegistry>(provider =>
            provider.GetRequiredService<ImageFormatRegistry>());
        services.AddSingleton<IImageDecoderResolver>(provider =>
            provider.GetRequiredService<ImageFormatRegistry>());
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
        services.AddSingleton<ClipboardImageWriter>();
        services.AddSingleton<IClipboardImageWriter>(provider =>
            provider.GetRequiredService<ClipboardImageWriter>());
        services.AddSingleton<IViewerClipboardWriter>(provider =>
            provider.GetRequiredService<ClipboardImageWriter>());
        services.AddSingleton<IImageViewerWindowFactory, ImageViewerWindowFactory>();

        return services;
    }
}
