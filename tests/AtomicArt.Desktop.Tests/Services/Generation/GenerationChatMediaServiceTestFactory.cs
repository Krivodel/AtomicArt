using Microsoft.Extensions.Options;

using Moq;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal static class GenerationChatMediaServiceTestFactory
{
    internal static GenerationChatMediaService Create(
        IGenerationReferenceStore references, IAtomicArtDataPathProvider paths,
        int maxInputImageBytes = TestApiConfiguration.MaxInputImageBytes,
        IAttachedImagePreparationService? preparation = null)
    {
        return new GenerationChatMediaService(
            references,
            Mock.Of<IFilePickerService>(),
            preparation ?? Mock.Of<IAttachedImagePreparationService>(),
            Mock.Of<IImageViewerService>(),
            new DataRootAccessCoordinator(),
            paths,
            TestApiConfiguration.CreateTrustedFileStreamFactory(),
            GenerationImageFormatRegistryTestFactory.Create(),
            Options.Create(new GenerationClientOptions { MaxInputImageBytes = maxInputImageBytes }));
    }
}
