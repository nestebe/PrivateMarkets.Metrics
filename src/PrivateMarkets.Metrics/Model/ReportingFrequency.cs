namespace PrivateMarkets.Metrics;

/// <summary>Spacing of the observation dates of a time series such as the J-curve.</summary>
public enum ReportingFrequency
{
    /// <summary>Calendar month ends.</summary>
    Monthly = 1,

    /// <summary>Calendar quarter ends (March, June, September, December).</summary>
    Quarterly = 3,

    /// <summary>Half-year ends (June, December).</summary>
    SemiAnnual = 6,

    /// <summary>Year ends.</summary>
    Annual = 12,
}
