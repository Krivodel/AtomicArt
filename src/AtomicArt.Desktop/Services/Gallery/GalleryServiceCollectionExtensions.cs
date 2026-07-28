using Microsoft.Extensions.DependencyInjection;

using Avalonia.Controls;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Services.Gallery;

internal static class GalleryServiceCollectionExtensions
{
    public static IServiceCollection AddGalleryServices(this IServiceCollection services)
    {
        services
            .AddOptions<GalleryOptions>()
            .BindConfiguration(GalleryOptions.SectionName)
            .Validate(
                GalleryOptions.IsValid,
                "Gallery configuration must include valid timing, size, and concurrency limits.")
            .ValidateOnStart();
        services.AddSingleton<GalleryThumbnailSpecification>();
        services.AddSingleton<GalleryThumbnailSizeCalculator>();
        services.AddSingleton<GalleryOrderTimestampPolicy>();
        services.AddSingleton<GalleryOrderTimestampNormalizer>();
        services.AddGallerySceneServices();
        services.AddGalleryComposition();
        services.AddGalleryViewModelServices();

        return services;
    }

    private static AnimatedGalleryScene CreateGalleryScene(IServiceScopeFactory scopeFactory, TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(topLevel);

        IServiceScope scope = scopeFactory.CreateScope();
        GallerySceneTopLevelContext context = scope.ServiceProvider.GetRequiredService<GallerySceneTopLevelContext>();
        context.Attach(topLevel);
        AnimatedGalleryScene scene = scope.ServiceProvider.GetRequiredService<AnimatedGalleryScene>();
        scene.AttachLifetime(scope);

        return scene;
    }

    private static IGenerationPreviewSession CreateGenerationPreviewSession(
        IServiceScopeFactory scopeFactory,
        TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(topLevel);

        IServiceScope scope = scopeFactory.CreateScope();

        try
        {
            GallerySceneTopLevelContext context =
                scope.ServiceProvider.GetRequiredService<GallerySceneTopLevelContext>();
            context.Attach(topLevel);
            GenerationPreviewSession session =
                scope.ServiceProvider.GetRequiredService<GenerationPreviewSession>();
            session.AttachLifetime(scope);

            return session;
        }
        catch (Exception)
        {
            scope.Dispose();
            throw;
        }
    }

    private static IServiceCollection AddGallerySceneServices(this IServiceCollection services)
    {
        services.AddScoped<GallerySceneTopLevelContext>();
        services.AddScoped<IGalleryPreviewBitmapLoader, GalleryPreviewBitmapLoader>();
        services.AddScoped<IGalleryPreviewBitmapProvider, GalleryPreviewBitmapProvider>();
        services.AddScoped<IGalleryCardControlFactory, GenerationCardControlFactory>();
        services.AddScoped<IUiFrameScheduler>(provider =>
            provider
                .GetRequiredService<IUiFrameSchedulerFactory>()
                .Create(provider.GetRequiredService<GallerySceneTopLevelContext>().TopLevel));
        services.AddScoped<GalleryPreviewSourceScheduler>();
        services.AddScoped<GenerationPreviewSession>();
        services.AddScoped<GalleryLayoutService>();
        services.AddScoped<UiAnimationScheduler>();
        services.AddScoped<GalleryOverlayEffects>();
        services.AddScoped<GalleryAppendAnimator>();
        services.AddScoped<GalleryExistingCardAnimator>();
        services.AddScoped<GallerySpawnRetargetAnimator>();
        services.AddScoped<GalleryRemoveAnimator>();
        services.AddScoped<GalleryMotionAnimator>();
        services.AddScoped<GalleryFrontGenerationRetargetWaiter>();
        services.AddGalleryOperationRunners();
        services.AddScoped<GalleryOperationRunnerRegistry>();
        services.AddScoped<IGalleryOperationRunnerRegistry>(provider =>
            provider.GetRequiredService<GalleryOperationRunnerRegistry>());
        services.AddScoped<GalleryOperationBatchDispatcher>();
        services.AddScoped<GalleryOperationQueueProcessor>();
        services.AddScoped<GalleryOperationCoordinator>();
        services.AddScoped<AnimatedGalleryScene>();

        return services;
    }

    private static IServiceCollection AddGalleryComposition(this IServiceCollection services)
    {
        services.AddSingleton<Func<TopLevel, AnimatedGalleryScene>>(provider =>
            topLevel => CreateGalleryScene(provider.GetRequiredService<IServiceScopeFactory>(), topLevel));
        services.AddSingleton<Func<TopLevel, IGenerationPreviewSession>>(provider =>
            topLevel => CreateGenerationPreviewSession(
                provider.GetRequiredService<IServiceScopeFactory>(),
                topLevel));
        services.AddSingleton<
            IGenerationPreviewSessionFactory,
            GenerationPreviewSessionFactory>();
        services.AddSingleton<IGallerySceneServicesFactory, GallerySceneServicesFactory>();
        services.AddSingleton<IAnimatedGallerySceneFactory, AnimatedGallerySceneFactory>();
        services.AddSingleton<AnimatedGalleryOperations>();
        services.AddSingleton<IAnimatedGalleryOperations>(provider =>
            provider.GetRequiredService<AnimatedGalleryOperations>());

        return services;
    }

    private static IServiceCollection AddGalleryViewModelServices(this IServiceCollection services)
    {
        services.AddSingleton<IGalleryItemDeletionService, GalleryItemDeletionService>();
        services.AddTransient<GalleryItemsController>();
        services.AddTransient(provider =>
        {
            GalleryItemsController itemsController =
                ActivatorUtilities.CreateInstance<GalleryItemsController>(provider);
            GalleryLifecycleViewStateController viewStateController =
                ActivatorUtilities.CreateInstance<GalleryLifecycleViewStateController>(
                    provider,
                    itemsController);
            IReadOnlyList<IGalleryLifecycleEventHandler> lifecycleEventHandlers =
                CreateGalleryLifecycleEventHandlers(provider, viewStateController);
            GalleryLifecycleController lifecycleController =
                ActivatorUtilities.CreateInstance<GalleryLifecycleController>(
                    provider,
                    viewStateController,
                    lifecycleEventHandlers);

            return ActivatorUtilities.CreateInstance<GalleryViewModel>(
                provider,
                viewStateController,
                itemsController,
                lifecycleController);
        });

        return services;
    }

    private static IReadOnlyList<IGalleryLifecycleEventHandler> CreateGalleryLifecycleEventHandlers(
        IServiceProvider provider,
        IGalleryLifecycleViewState viewState)
    {
        Type handlerType = typeof(IGalleryLifecycleEventHandler);
        IReadOnlyList<Type> handlerTypes =
            DesktopTypeDiscovery.FindAllImplementations(handlerType);

        return handlerTypes
            .Select(type => (IGalleryLifecycleEventHandler)ActivatorUtilities.CreateInstance(
                provider,
                type,
                viewState))
            .ToList();
    }

    private static IServiceCollection AddGalleryOperationRunners(this IServiceCollection services)
    {
        Type runnerType = typeof(IGalleryOperationRunner);
        IReadOnlyList<Type> runnerTypes =
            DesktopTypeDiscovery.FindAllImplementations(runnerType);

        foreach (Type runner in runnerTypes)
        {
            services.AddScoped(runnerType, runner);
        }

        return services;
    }
}
