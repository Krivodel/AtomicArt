using System.Collections.ObjectModel;
using System.Collections.Specialized;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Helpers;

namespace AtomicArt.Desktop.Tests.Helpers;

public sealed class ObservableCollectionSynchronizerTests
{
    [Fact]
    public void Synchronize_WithUnchangedItems_DoesNotNotify()
    {
        object first = new();
        object second = new();
        ObservableCollection<object> collection = [first, second];
        int notifications = 0;
        collection.CollectionChanged += (_, _) => notifications++;

        ObservableCollectionSynchronizer.Synchronize(collection, new object[] { first, second });

        notifications.Should().Be(0);
    }

    [Fact]
    public void Synchronize_WithRemovedAddedAndMovedItems_PreservesObjectsWithoutResetting()
    {
        object first = new();
        object removed = new();
        object last = new();
        object added = new();
        ObservableCollection<object> collection = [first, removed, last];
        List<NotifyCollectionChangedAction> notifications = [];
        collection.CollectionChanged += (_, e) => notifications.Add(e.Action);

        ObservableCollectionSynchronizer.Synchronize(collection, new object[] { last, added, first });

        collection.Should().Equal(new object[] { last, added, first });
        notifications.Should().NotContain(NotifyCollectionChangedAction.Reset);
    }

    [Theory]
    [InlineData(new string[] { "first", "second" }, new string[] { "first", "first", "second" })]
    [InlineData(new string[] { "first", "first", "second" }, new string[] { "first", "second" })]
    public void Synchronize_WithRepeatedValues_PreservesTheirRequestedCount(string[] initial, string[] desired)
    {
        ObservableCollection<string> collection = new(initial);

        ObservableCollectionSynchronizer.Synchronize(collection, desired);

        collection.Should().Equal(desired);
    }
}
