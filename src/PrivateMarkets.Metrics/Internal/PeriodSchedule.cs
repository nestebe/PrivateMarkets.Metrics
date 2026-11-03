namespace PrivateMarkets.Metrics.Internal;

/// <summary>Generates calendar period ends.</summary>
internal static class PeriodSchedule
{
    /// <summary>
    /// Period ends of <paramref name="frequency"/> falling in [<paramref name="start"/>, <paramref name="end"/>],
    /// starting with the period containing <paramref name="start"/> and always ending with <paramref name="end"/>.
    /// </summary>
    public static IEnumerable<DateOnly> PeriodEnds(DateOnly start, DateOnly end, ReportingFrequency frequency)
    {
        if (!Enum.IsDefined(frequency))
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown reporting frequency.");
        }

        if (end < start)
        {
            yield break;
        }

        var months = (int)frequency;
        var periodEndMonth = ((start.Month - 1) / months * months) + months;
        var current = EndOfMonth(start.Year, periodEndMonth);
        while (current < end)
        {
            yield return current;
            var next = current.AddMonths(months);
            current = EndOfMonth(next.Year, next.Month);
        }

        yield return end;
    }

    private static DateOnly EndOfMonth(int year, int month) => new(year, month, DateTime.DaysInMonth(year, month));
}
