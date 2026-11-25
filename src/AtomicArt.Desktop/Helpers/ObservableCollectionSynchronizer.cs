using System.Collections.ObjectModel;

namespace AtomicArt.Desktop.Helpers;

internal static class ObservableCollectionSynchronizer
{
    public static void Synchronize<T>(ObservableCollection<T> collection, IReadOnlyList<T> desired)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(desired);

        if (collection.SequenceEqual(desired))
        {
            return;
        }

        HashSet<T> desiredItems = desired.ToHashSet();

        for (int index = collection.Count - 1; index >= 0; index--)
        {
            if (!desiredItems.Contains(collection[index]))
            {
                collection.RemoveAt(index);
            }
        }

        for (int index = 0; index < desired.Count; index++)
        {
            if (index < collection.Count && EqualityComparer<T>.Default.Equals(collection[index], desired[index]))
            {
                continue;
            }

            int previous = collection.IndexOf(desired[index]);

            if (previous < index)
            {
                collection.Insert(index, desired[index]);
            }
            else
            {
                collection.Move(previous, index);
            }
        }

        while (collection.Count > desired.Count)
        {
            collection.RemoveAt(collection.Count - 1);
        }
    }
}
