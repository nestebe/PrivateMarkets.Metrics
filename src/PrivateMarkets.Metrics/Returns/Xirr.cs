namespace PrivateMarkets.Metrics.Returns;

/// <summary>
/// Internal rate of return of irregularly dated cash flows (Excel <c>XIRR</c> semantics):
/// the annual rate <c>r</c> such that <c>Σ aᵢ (1 + r)^(−tᵢ) = 0</c>, where <c>tᵢ</c> is the year
/// fraction between the first date and the date of flow <c>i</c>.
/// </summary>
/// <remarks>
/// The solver runs Newton-Raphson from the guess and falls back to bisection over a scanned bracket
/// when Newton fails to converge. Computations run in <see cref="double"/>; results are converted
/// back to <see cref="decimal"/>.
/// </remarks>
public static class Xirr
{
    private const double MinimumRate = -0.999_999_999;
    private const double MaximumRate = 1e6;
    private const double RateTolerance = 1e-12;
    private const double RelativeNpvTolerance = 1e-9;
    private const int MaxNewtonIterations = 100;
    private const int MaxBisectionIterations = 300;
    private const int ResultDecimals = 12;

    /// <summary>Computes the IRR of the cash flows.</summary>
    /// <param name="cashFlows">Signed cash flows (negative = investment, positive = return).</param>
    /// <param name="dayCount">Day count convention used for year fractions.</param>
    /// <param name="guess">Starting point of the Newton iterations.</param>
    /// <returns>
    /// The annual rate rounded to 12 decimals (the solver tolerance), or <see langword="null"/> when no
    /// rate exists (for instance all flows have the same sign).
    /// </returns>
    public static decimal? Calculate(
        IEnumerable<DatedAmount> cashFlows,
        DayCountConvention dayCount = DayCountConvention.Actual365Fixed,
        double guess = 0.1)
        => TryCalculate(cashFlows, out var rate, dayCount, guess) ? RoundToTolerance(rate) : null;

    /// <summary>Computes the IRR of the cash flows without throwing when no solution exists.</summary>
    /// <param name="cashFlows">Signed cash flows (negative = investment, positive = return).</param>
    /// <param name="rate">The annual rate when the method returns <see langword="true"/>; otherwise <see cref="double.NaN"/>.</param>
    /// <param name="dayCount">Day count convention used for year fractions.</param>
    /// <param name="guess">Starting point of the Newton iterations.</param>
    /// <returns><see langword="true"/> when a rate was found.</returns>
    public static bool TryCalculate(
        IEnumerable<DatedAmount> cashFlows,
        out double rate,
        DayCountConvention dayCount = DayCountConvention.Actual365Fixed,
        double guess = 0.1)
    {
        ArgumentNullException.ThrowIfNull(cashFlows);

        var schedule = Schedule.From(cashFlows, dayCount);
        if (schedule.HasSolution && (TryNewton(schedule, guess, out rate) || TryBisection(schedule, guess, out rate)))
        {
            return true;
        }

        rate = double.NaN;
        return false;
    }

    /// <summary>Net present value of the cash flows at <paramref name="rate"/>, discounted to the first date (Excel <c>XNPV</c>).</summary>
    /// <param name="cashFlows">Signed cash flows.</param>
    /// <param name="rate">Annual discount rate, strictly greater than −100%.</param>
    /// <param name="dayCount">Day count convention used for year fractions.</param>
    /// <exception cref="ArgumentOutOfRangeException">The rate is −100% or lower.</exception>
    public static decimal NetPresentValue(
        IEnumerable<DatedAmount> cashFlows,
        double rate,
        DayCountConvention dayCount = DayCountConvention.Actual365Fixed)
    {
        ArgumentNullException.ThrowIfNull(cashFlows);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rate, -1d);
        return (decimal)Schedule.From(cashFlows, dayCount).Npv(rate);
    }

    /// <summary>Rounds to the solver tolerance and drops trailing zeros (0.000000000000 becomes 0).</summary>
    private static decimal RoundToTolerance(double rate) =>
        Math.Round((decimal)rate, ResultDecimals) / 1.000000000000000000000000000000000m;

    private static bool TryNewton(Schedule schedule, double guess, out double rate)
    {
        rate = guess;
        for (var iteration = 0; iteration < MaxNewtonIterations; iteration++)
        {
            var (npv, derivative) = schedule.NpvAndDerivative(rate);
            if (!double.IsFinite(npv) || !double.IsFinite(derivative) || derivative == 0d)
            {
                return false;
            }

            var next = rate - (npv / derivative);
            if (next <= -1d)
            {
                // Keep the iterate inside the domain by moving halfway towards -100%.
                next = (rate - 1d) / 2d;
            }

            if (Math.Abs(next - rate) <= RateTolerance * Math.Max(1d, Math.Abs(rate)))
            {
                rate = next;
                return schedule.IsRoot(rate);
            }

            rate = next;
        }

        return false;
    }

    private static bool TryBisection(Schedule schedule, double guess, out double rate)
    {
        rate = double.NaN;
        if (!TryFindBracket(schedule, guess, out var low, out var high))
        {
            return false;
        }

        var npvLow = schedule.Npv(low);
        for (var iteration = 0; iteration < MaxBisectionIterations; iteration++)
        {
            var middle = low + ((high - low) / 2d);
            var npvMiddle = schedule.Npv(middle);
            if (npvMiddle == 0d || (high - low) / 2d <= RateTolerance * Math.Max(1d, Math.Abs(middle)))
            {
                rate = middle;
                return schedule.IsRoot(rate);
            }

            if (Math.Sign(npvMiddle) == Math.Sign(npvLow))
            {
                low = middle;
                npvLow = npvMiddle;
            }
            else
            {
                high = middle;
            }
        }

        return false;
    }

    /// <summary>Scans a grid of rates and keeps the sign-changing interval closest to the guess.</summary>
    private static bool TryFindBracket(Schedule schedule, double guess, out double low, out double high)
    {
        low = high = double.NaN;
        var bestDistance = double.PositiveInfinity;
        double? previousRate = null;
        var previousNpv = double.NaN;

        foreach (var candidate in BracketGrid())
        {
            var npv = schedule.Npv(candidate);
            if (!double.IsFinite(npv))
            {
                previousRate = null;
                continue;
            }

            if (previousRate is { } previous && Math.Sign(npv) != Math.Sign(previousNpv))
            {
                var distance = Math.Abs(((previous + candidate) / 2d) - guess);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    low = previous;
                    high = candidate;
                }
            }

            previousRate = candidate;
            previousNpv = npv;
        }

        return !double.IsNaN(low);
    }

    private static IEnumerable<double> BracketGrid()
    {
        yield return MinimumRate;
        for (var step = -99; step < 100; step++)
        {
            yield return step / 100d;
        }

        for (var rate = 1d; rate <= MaximumRate; rate *= 1.25)
        {
            yield return rate;
        }
    }

    /// <summary>Cash flows normalised as (year fraction, amount) pairs.</summary>
    private sealed class Schedule
    {
        private readonly double[] _times;
        private readonly double[] _amounts;
        private readonly double _scale;

        private Schedule(double[] times, double[] amounts)
        {
            _times = times;
            _amounts = amounts;
            _scale = amounts.Sum(Math.Abs);
        }

        public bool HasSolution =>
            _amounts.Any(a => a > 0d) && _amounts.Any(a => a < 0d) && _times.Any(t => t != 0d);

        public static Schedule From(IEnumerable<DatedAmount> cashFlows, DayCountConvention dayCount)
        {
            var flows = cashFlows.ToArray();
            if (flows.Length == 0)
            {
                return new Schedule([], []);
            }

            var origin = flows.Min(f => f.Date);
            var times = flows.Select(f => dayCount.YearFraction(origin, f.Date)).ToArray();
            var amounts = flows.Select(f => (double)f.Amount).ToArray();
            return new Schedule(times, amounts);
        }

        public double Npv(double rate)
        {
            var growth = 1d + rate;
            var npv = 0d;
            for (var i = 0; i < _amounts.Length; i++)
            {
                npv += _amounts[i] * Math.Pow(growth, -_times[i]);
            }

            return npv;
        }

        public (double Npv, double Derivative) NpvAndDerivative(double rate)
        {
            var growth = 1d + rate;
            double npv = 0d, derivative = 0d;
            for (var i = 0; i < _amounts.Length; i++)
            {
                var discounted = _amounts[i] * Math.Pow(growth, -_times[i]);
                npv += discounted;
                derivative -= _times[i] * discounted / growth;
            }

            return (npv, derivative);
        }

        public bool IsRoot(double rate) =>
            rate > -1d && double.IsFinite(rate) && Math.Abs(Npv(rate)) <= RelativeNpvTolerance * _scale;
    }
}
