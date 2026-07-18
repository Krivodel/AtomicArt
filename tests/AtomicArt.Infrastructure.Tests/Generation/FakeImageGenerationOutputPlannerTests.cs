using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class FakeImageGenerationOutputPlannerTests
{
    private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void CreatePlan_WithGenerationCount_ReturnsRequestedItemCount()
    {
        FakeImageGenerationOutputPlanner planner = new();
        ImageGenerationRequestDto request = CreateRequest(generationCount: 3);

        ImageGenerationOutputPlan plan = planner.CreatePlan(
            request,
            BatchId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName);

        plan.Items.Should().HaveCount(3);
    }

    [Fact]
    public void CreatePlan_WithRequest_CreatesItemsWithoutStoragePaths()
    {
        FakeImageGenerationOutputPlanner planner = new();
        ImageGenerationRequestDto request = CreateRequest();

        ImageGenerationOutputPlan plan = planner.CreatePlan(
            request,
            BatchId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName);

        ImageGenerationOutputItemPlan item = plan.Items.Single();
        item.Id.Should().NotBe(Guid.Empty);
        item.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static ImageGenerationRequestDto CreateRequest(int generationCount = 1)
    {
        return ImageGenerationRequestDtoTestFactory.Create(
            aspectRatio: "Авто",
            resolution: "1k",
            generationCount: generationCount);
    }
}
