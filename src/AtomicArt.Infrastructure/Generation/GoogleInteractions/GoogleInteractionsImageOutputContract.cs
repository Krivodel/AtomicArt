using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal static class GoogleInteractionsImageOutputContract
{
    internal const string ContentType = GenerationImageContentTypes.Jpeg;
    internal const string SystemInstruction =
        "Treat **EVERY user input as an image generation request**. Return **image output only**. DO NOT answer with explanatory text.";
}
