using System.Text;
using System.Text.Json.Nodes;
using Logistics.Runner;
using Meitou.Simulation;

namespace Logistics.Core.Tests;

/// <summary>
/// The save sidecar (docs/logistics-core.md "Save") and the runner's determinism (docs/balance-runner.md): a save and load mid-run changes nothing.
/// </summary>
public class SaveTests
{
    const string Stack = "town:9-test";

    static LogisticsNetwork Network(params string[] allowed) =>
        new([new NodeSeed(Stack, "Stack", NodeKind.Ownable, "hn-f", allowed.Length > 0 ? allowed : ["hn-f", "shek-f"])], new OwnerRules(), 100, _ => { });

    static ulong Hash(LogisticsNetwork n)
    {
        var hasher = new StateHasher();
        n.Hash(ref hasher);
        return hasher.Value;
    }

    static MemoryStream Saved(LogisticsNetwork network, IReadOnlyList<JsonObject>? kept = null)
    {
        var stream = new MemoryStream();
        NetworkSave.Write(network, kept ?? [], stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Owners_penalties_the_clock_and_queued_events_come_back_from_a_save()
    {
        var network = Network();
        network.RequestCapture(Stack, "kral-f");
        network.AdvanceTo(105);
        network.RequestCapture(Stack, "shek-f");

        var loaded = Network();
        NetworkSave.Read(loaded, Saved(network), _ => { });

        Assert.Equal(Hash(network), Hash(loaded));
        Assert.Equal(network.Nodes(), loaded.Nodes(), (a, b) => a.Owner == b.Owner && a.PenaltyUntilHour == b.PenaltyUntilHour);
        Assert.Equal(network.TakeEvents(), loaded.TakeEvents());
    }

    [Fact]
    public void A_saved_node_the_world_lacks_is_kept_and_written_again()
    {
        var other = new LogisticsNetwork([new NodeSeed("gone.mod:fort", "Fort", NodeKind.Ownable, "x-f", ["x-f"])], new OwnerRules(), 100, _ => { });
        var kept = NetworkSave.Read(Network(), Saved(other), _ => { });

        string again = Encoding.UTF8.GetString(Saved(Network(), kept).ToArray());

        Assert.Single(kept);
        Assert.Contains("gone.mod:fort", again);
    }

    [Fact]
    public void A_saved_holder_no_longer_allowed_reverts_to_the_seeded_holder()
    {
        var network = Network("hn-f", "nomad-f");
        network.RequestCapture(Stack, "nomad-f");
        var log = new List<string>();

        var loaded = Network();
        NetworkSave.Read(loaded, Saved(network), log.Add);

        Assert.Equal("hn-f", loaded.Node(Stack)!.Owner);
        Assert.Contains(log, m => m.Contains("nomad-f is no longer allowed"));
    }

    [Fact]
    public void A_save_from_a_newer_schema_leaves_the_network_as_seeded()
    {
        var log = new List<string>();
        var loaded = Network();

        NetworkSave.Read(loaded, new MemoryStream("""{"schema":99,"hour":500,"nodes":[]}"""u8.ToArray()), log.Add);

        Assert.Equal(100, loaded.Hour);
        Assert.Single(log);
    }

    [Fact]
    public void The_runner_gives_the_same_numbers_with_or_without_a_save_and_load_mid_run()
    {
        var straight = BalanceRunner.Run(new RunOptions(Days: 6));
        var reloaded = BalanceRunner.Run(new RunOptions(Days: 6, SaveAtHour: 61));

        Assert.Equal(straight.Hash, reloaded.Hash);
        Assert.Equal(6, straight.Days.Count);
        Assert.Contains(straight.Days, d => d.Events.ContainsKey(nameof(PenaltyStarted)));
    }

    [Fact]
    public void The_same_seed_gives_the_same_run_and_another_seed_another()
    {
        Assert.Equal(BalanceRunner.Run(new RunOptions(Days: 3)).Hash, BalanceRunner.Run(new RunOptions(Days: 3)).Hash);
        Assert.NotEqual(BalanceRunner.Run(new RunOptions(Days: 3)).Hash, BalanceRunner.Run(new RunOptions(Days: 3, Seed: 2)).Hash);
    }
}
