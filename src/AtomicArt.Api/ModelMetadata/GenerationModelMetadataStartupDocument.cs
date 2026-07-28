using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Api.ModelMetadata;

public sealed record GenerationModelMetadataStartupDocument(
    GenerationModelCatalogDto Catalog,
    TestGenerationModelMetadata? TestModel);
