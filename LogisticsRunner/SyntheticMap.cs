using Logistics.Core;
using Meitou.Simulation;

namespace Logistics.Runner;

/// <summary>
/// A made-up map for the runner until the real map's seeding is agreed: towns spread over a few factions, each allowing its holder and one or two
/// rivals, plus destroyable nests. The same seed gives the same map.
/// </summary>
public static class SyntheticMap
{
    /// <summary>
    /// The factions the towns go to.
    /// </summary>
    public static readonly string[] Factions = ["faction:holy", "faction:shek", "faction:traders", "faction:united", "faction:kral"];

    /// <summary>
    /// A network of <paramref name="towns"/> ownable towns and <paramref name="nests"/> nests at <paramref name="hour"/>.
    /// </summary>
    public static LogisticsNetwork Build(int towns, int nests, ulong seed, long hour)
    {
        var seeds = Enumerable.Range(0, towns).Select(i => Town(i, seed))
            .Concat(Enumerable.Range(0, nests).Select(i => new NodeSeed($"synthetic:nest-{i:D3}", $"Nest {i}", NodeKind.Destroyable, null, [])));
        return new LogisticsNetwork(seeds, new OwnerRules(), hour, _ => { });
    }

    /// <summary>
    /// Town <paramref name="i"/>: a holder, and rivals that may take it.
    /// </summary>
    static NodeSeed Town(int i, ulong seed)
    {
        int holder = Roll(seed, i, 0, Factions.Length);
        var allowed = new SortedSet<string>(StringComparer.Ordinal) { Factions[holder] };
        int rivals = 1 + Roll(seed, i, 1, 2);
        for (int r = 0; r < rivals; r++) allowed.Add(Factions[Roll(seed, i, 2 + r, Factions.Length)]);

        return new NodeSeed($"synthetic:town-{i:D3}", $"Town {i}", NodeKind.Ownable, Factions[holder], [.. allowed]);
    }

    /// <summary>
    /// A roll below <paramref name="bound"/> for town <paramref name="i"/>'s <paramref name="slot"/>.
    /// </summary>
    static int Roll(ulong seed, int i, int slot, int bound) => Rng.Int(Rng.Hash(seed, (ulong)i, RngPurpose.Test, (ulong)slot), bound);
}

/// <summary>
/// The default policy: each hour, with a 1 in 6 chance, a random faction tries to take a random town. Refusals bring the penalty, as a flip
/// War Orchestration wanted but the owner list forbids would.
/// </summary>
public sealed class RandomCaptures : IPolicy
{
    /// <inheritdoc />
    public void Hour(LogisticsNetwork network, Func<int, int> roll)
    {
        if (roll(6) != 0) return;

        var towns = network.Nodes().Where(n => n.Kind == NodeKind.Ownable).ToArray();
        if (towns.Length == 0) return;

        network.RequestCapture(towns[roll(towns.Length)].Id, SyntheticMap.Factions[roll(SyntheticMap.Factions.Length)]);
    }
}
