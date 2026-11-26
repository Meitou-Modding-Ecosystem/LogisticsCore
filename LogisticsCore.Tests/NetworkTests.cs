using Logistics.Seed;
using Meitou.Data.Gameplay;
using Meitou.Simulation;

namespace Logistics.Core.Tests;

/// <summary>
/// The node network (docs/logistics-core.md): allowed owners and the dependents' changes, captures, the refused-flip penalty and the event queue.
/// Stack is held by the Holy Nation and may go to the Shek; the den is a nest.
/// </summary>
public class NetworkTests
{
    const string Holy = "hn-f", Shek = "shek-f", Kral = "kral-f";
    const string Stack = "town:9-test", Den = "town:12-test";

    static readonly NodeSeed[] Seeds =
    [
        new(Stack, "Stack", NodeKind.Ownable, Holy, [Holy, Shek]),
        new(Den, "Beak Den", NodeKind.Destroyable, "beasts-f", ["beasts-f"]),
    ];

    static LogisticsNetwork Network(OwnerRules? rules = null, List<string>? log = null) =>
        new(Seeds, rules ?? new OwnerRules(), 100, m => log?.Add(m));

    [Fact]
    public void Owner_changes_from_two_mods_both_apply_and_a_disallow_is_logged_but_never_removes_the_holder()
    {
        var rules = new OwnerRules();
        rules.Add("a.mod", Stack, Kral, allow: true);
        rules.Add("b.mod", Stack, "nomad-f", allow: true);
        rules.Add("b.mod", Stack, Shek, allow: false);
        rules.Add("b.mod", Stack, Holy, allow: false);
        rules.Add("b.mod", "town:missing", Kral, allow: true);
        var log = new List<string>();

        var stack = Network(rules, log).Node(Stack)!;

        Assert.Equal([Holy, Kral, "nomad-f"], stack.AllowedOwners);
        Assert.Contains(log, m => m.Contains("disallowed shek-f on town:9-test"));
        Assert.Contains(log, m => m.Contains("which holds it: kept"));
        Assert.Contains(log, m => m.Contains("town:missing"));
    }

    [Fact]
    public void Registration_closes_when_frozen()
    {
        var rules = new OwnerRules();
        rules.Freeze();

        Assert.Throws<InvalidOperationException>(() => rules.Add("a.mod", Stack, Kral, allow: true));
    }

    [Fact]
    public void A_seed_whose_holder_is_not_listed_keeps_it_allowed_and_a_duplicate_id_is_refused()
    {
        var network = new LogisticsNetwork([new NodeSeed("x:a", "A", NodeKind.Ownable, Holy, [Shek])], new OwnerRules(), 0, _ => { });

        Assert.Equal([Holy, Shek], network.Node("x:a")!.AllowedOwners);
        Assert.Throws<ArgumentException>(() => new LogisticsNetwork([Seeds[0], Seeds[0]], new OwnerRules(), 0, _ => { }));
    }

    [Fact]
    public void An_allowed_faction_captures_and_the_change_is_queued()
    {
        var network = Network();

        Assert.Equal(CaptureResult.Captured, network.RequestCapture(Stack, Shek));
        Assert.Equal(CaptureResult.AlreadyOwner, network.RequestCapture(Stack, Shek));

        Assert.Equal(Shek, network.Node(Stack)!.Owner);
        Assert.Equal([new OwnerChanged(100, Stack, Holy, Shek)], network.TakeEvents());
        Assert.Empty(network.TakeEvents());
    }

    [Fact]
    public void A_flip_the_owner_list_forbids_is_refused_and_penalizes_the_town_for_24_hours()
    {
        var network = Network();

        Assert.Equal(CaptureResult.NotAllowed, network.RequestCapture(Stack, Kral));

        var stack = network.Node(Stack)!;
        Assert.Equal(Holy, stack.Owner);
        Assert.Equal(124, stack.PenaltyUntilHour);
        Assert.True(network.IsPenalized(Stack));
        Assert.Equal([new CaptureRefused(100, Stack, Kral, CaptureResult.NotAllowed), new PenaltyStarted(100, Stack, 124)], network.TakeEvents());
    }

    [Fact]
    public void The_penalty_ends_after_24_hours_and_a_refusal_during_it_restarts_the_clock()
    {
        var network = Network();
        network.RequestCapture(Stack, Kral);
        network.AdvanceTo(110);
        network.RequestCapture(Stack, Kral);
        network.TakeEvents();

        network.AdvanceTo(133);
        Assert.True(network.IsPenalized(Stack));

        network.AdvanceTo(134);
        Assert.False(network.IsPenalized(Stack));
        Assert.Equal([new PenaltyEnded(134, Stack)], network.TakeEvents());
    }

    [Fact]
    public void A_destroyable_node_refuses_every_capture_without_a_penalty()
    {
        var network = Network();

        Assert.Equal(CaptureResult.NotOwnable, network.RequestCapture(Den, Holy));
        Assert.Equal(CaptureResult.UnknownNode, network.RequestCapture("town:none", Holy));

        Assert.False(network.IsPenalized(Den));
        Assert.Equal([new CaptureRefused(100, Den, Holy, CaptureResult.NotOwnable)], network.TakeEvents());
    }

    [Fact]
    public void No_node_is_ever_held_outside_its_allowed_owners()
    {
        var network = Network();
        foreach (string faction in new[] { Kral, Shek, "beasts-f", Holy, "nomad-f" })
            foreach (var node in network.Nodes())
                network.RequestCapture(node.Id, faction);

        Assert.All(network.Nodes(), n => Assert.Contains(n.Owner!, n.AllowedOwners));
    }

    [Fact]
    public void Vanilla_towns_are_ownable_and_nests_destroyable()
    {
        VanillaNode Vanilla(string id, TownType kind, bool ownable) =>
            new(id, id, id, kind, default, Holy, [Holy], [], ownable);

        var seeds = LogisticsNetwork.Vanilla([Vanilla("town:1", TownType.Town, true), Vanilla("town:2", TownType.Nest, false)]).ToList();

        Assert.Equal([NodeKind.Ownable, NodeKind.Destroyable], seeds.Select(s => s.Kind));
    }

    [Fact]
    public void The_hash_sees_owners_penalties_and_queued_events()
    {
        static ulong Hash(LogisticsNetwork n)
        {
            var hasher = new StateHasher();
            n.Hash(ref hasher);
            return hasher.Value;
        }

        var a = Network();
        var b = Network();
        Assert.Equal(Hash(a), Hash(b));

        b.RequestCapture(Stack, Shek);
        Assert.NotEqual(Hash(a), Hash(b));

        a.RequestCapture(Stack, Shek);
        a.TakeEvents();
        Assert.NotEqual(Hash(a), Hash(b));
    }
}
