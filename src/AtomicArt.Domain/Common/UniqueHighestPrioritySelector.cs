namespace AtomicArt.Domain.Common;

public static class UniqueHighestPrioritySelector
{
    public static T Select<T>(
        IEnumerable<T> candidates,
        Func<T, bool> isMatch,
        Func<T, int> getPriority,
        Func<Exception> createNoMatchException,
        Func<int, Exception> createTieException)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(isMatch);
        ArgumentNullException.ThrowIfNull(getPriority);
        ArgumentNullException.ThrowIfNull(createNoMatchException);
        ArgumentNullException.ThrowIfNull(createTieException);

        List<T> matchingCandidates = candidates
            .Where(isMatch)
            .OrderByDescending(getPriority)
            .ToList();

        if (matchingCandidates.Count == 0)
        {
            throw createNoMatchException();
        }

        T selectedCandidate = matchingCandidates[0];

        if (matchingCandidates.Count > 1
            && getPriority(matchingCandidates[1]) == getPriority(selectedCandidate))
        {
            throw createTieException(getPriority(selectedCandidate));
        }

        return selectedCandidate;
    }
}
