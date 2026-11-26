using Logistics.Core;
using Meitou.Simulation;

namespace Logistics.Runner;

/// <summary>
/// What to run: the map's size, the seed, how long, the policy, and an hour at which to save and load the network mid-run (determinism check).
/// </summary>
public sealed record RunOptions(int Towns = 40, int Nests = 8, ulong Seed = 1, int Days = 30, int? SaveAtHour = null, IPolicy? Policy = null);

/// <summary>
/// One day's numbers: towns held per faction, nodes under the refused-flip penalty, and the events of that day by kind.
/// </summary>
public sealed record DayReport(int Day, IReadOnlyDictionary<string, int> Held, int Penalized, IReadOnlyDictionary<string, int> Events);

/// <summary>
/// A run's result: the days and the network's state hash at the end.
/// </summary>
public sealed record RunReport(IReadOnlyList<DayReport> Days, ulong Hash);

/// <summary>
/// What drives the network each hour in place of War Orchestration's rules (pressures, captures). It may only call the network's public members.
/// </summary>
public interface IPolicy
{
    /// <summary>
    /// Acts on <paramref name="network"/> at its current hour; <paramref name="roll"/> gives the hour's deterministic random numbers.
    /// </summary>
    void Hour(LogisticsNetwork network, Func<int, int> roll);
}

/// <summary>
/// Runs a network headless (docs/balance-runner.md).
/// </summary>
public static class BalanceRunner
{
    /// <summary>
    /// The game hour the runs start at (day 1, 13:00, as a new game).
    /// </summary>
    public const long StartHour = 37;

    /// <summary>
    /// Runs <paramref name="options"/>.
    /// </summary>
    public static RunReport Run(RunOptions options)
    {
        var network = SyntheticMap.Build(options.Towns, options.Nests, options.Seed, StartHour);
        var policy = options.Policy ?? new RandomCaptures();
        var days = new List<DayReport>();
        var events = new Dictionary<string, int>(StringComparer.Ordinal);

        for (long hour = StartHour; hour < StartHour + options.Days * 24L; hour++)
        {
            if (hour == StartHour + options.SaveAtHour) network = SaveAndLoad(network, options);
            Step(network, policy, options.Seed, hour, events);
            if ((hour - StartHour + 1) % 24 == 0) days.Add(Day(network, days.Count + 1, events));
        }

        var hasher = new StateHasher();
        network.Hash(ref hasher);
        return new RunReport(days, hasher.Value);
    }

    /// <summary>
    /// One hour: the clock moves, the policy acts, and the hour's events are counted (as the game delivers them).
    /// </summary>
    static void Step(LogisticsNetwork network, IPolicy policy, ulong seed, long hour, Dictionary<string, int> events)
    {
        network.AdvanceTo(hour);
        ulong counter = 0;
        policy.Hour(network, bound => Rng.Int(Rng.Hash(seed, (ulong)hour, RngPurpose.Test, counter++), bound));
        foreach (var e in network.TakeEvents()) events[e.GetType().Name] = events.GetValueOrDefault(e.GetType().Name) + 1;
    }

    /// <summary>
    /// The day's numbers; the event counts start again.
    /// </summary>
    static DayReport Day(LogisticsNetwork network, int day, Dictionary<string, int> events)
    {
        var nodes = network.Nodes();
        var held = nodes
            .Where(n => n.Kind == NodeKind.Ownable && n.Owner is not null)
            .GroupBy(n => n.Owner!, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var report = new DayReport(day, held, nodes.Count(n => n.PenaltyUntilHour is not null), new Dictionary<string, int>(events));
        events.Clear();
        return report;
    }

    /// <summary>
    /// The network written to a save and read into a freshly seeded one, as a game save and load would.
    /// </summary>
    static LogisticsNetwork SaveAndLoad(LogisticsNetwork network, RunOptions options)
    {
        using var stream = new MemoryStream();
        NetworkSave.Write(network, [], stream);
        stream.Position = 0;
        var loaded = SyntheticMap.Build(options.Towns, options.Nests, options.Seed, StartHour);
        NetworkSave.Read(loaded, stream, Console.WriteLine);
        return loaded;
    }
}
