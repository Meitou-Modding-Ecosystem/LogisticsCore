using System.Text.Json;
using Meitou.Modding;

namespace Logistics.Core.Tests;

/// <summary>
/// The mod entry as dependents use it (docs/logistics-core.md "Using Logistics Core"): handles, subscriptions in (order, id) order, captures
/// through a handle, a failing handler or dependent dropped, and the manifest agreeing with the code.
/// </summary>
public class EntryTests
{
    const string Stack = "town:9-test";

    static LogisticsNetwork Network(LogisticsCoreEntry core) =>
        new([new NodeSeed(Stack, "Stack", NodeKind.Ownable, "hn-f", ["hn-f", "shek-f"])], core.Rules, 100, _ => { });

    [Fact]
    public void The_manifest_names_the_entry_its_id_and_the_exported_contract()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "meitou.json")));
        var root = manifest.RootElement;

        var entry = typeof(LogisticsCoreEntry).Assembly.GetType(root.GetProperty("entry").GetString()!);

        Assert.NotNull(entry);
        Assert.True(typeof(IMod).IsAssignableFrom(entry));
        Assert.True(typeof(ILogisticsApi).IsAssignableFrom(entry));
        Assert.Equal(LogisticsCoreEntry.Id, root.GetProperty("id").GetString());
        Assert.Equal(["LogisticsCore.Contracts.dll", "LogisticsCore.dll"], root.GetProperty("assemblies").EnumerateArray().Select(a => a.GetString()));
    }

    [Fact]
    public void Handlers_run_in_order_then_id_and_one_that_throws_is_dropped_for_that_world()
    {
        var core = new LogisticsCoreEntry();
        var seen = new List<string>();
        core.For("b.mod").Subscribe("b.mod:late", 10, e => seen.Add("b.late"));
        core.For("b.mod").Subscribe("b.mod:early", 0, e => seen.Add("b.early"));
        core.For("a.mod").Subscribe("a.mod:early", 0, e => throw new InvalidOperationException("boom"));
        var system = core.Build(Network(core));

        Assert.Equal(CaptureResult.Captured, core.For("x.mod").RequestCapture(Stack, "shek-f"));
        system.Step(100);
        core.For("x.mod").RequestCapture(Stack, "hn-f");
        system.Step(101);

        Assert.Equal(["b.early", "b.late", "b.early", "b.late"], seen);
    }

    [Fact]
    public void Ids_need_the_mods_prefix_and_registration_closes_with_the_first_world()
    {
        var core = new LogisticsCoreEntry();
        var mod = core.For("a.mod");
        mod.Subscribe("a.mod:one", 0, _ => { });

        Assert.Same(mod, core.For("a.mod"));
        Assert.Throws<ArgumentException>(() => mod.Subscribe("b.mod:one", 0, _ => { }));
        Assert.Throws<ArgumentException>(() => mod.Subscribe("a.mod:", 0, _ => { }));
        Assert.Throws<ArgumentException>(() => mod.Subscribe("a.mod:one", 0, _ => { }));
        Assert.Throws<InvalidOperationException>(() => mod.RequestCapture(Stack, "shek-f"));

        core.Build(Network(core));
        Assert.Throws<InvalidOperationException>(() => mod.AllowOwner(Stack, "kral-f"));
        Assert.Throws<InvalidOperationException>(() => mod.Subscribe("a.mod:two", 0, _ => { }));
    }

    [Fact]
    public void A_failed_dependents_owner_changes_and_handlers_are_dropped()
    {
        var core = new LogisticsCoreEntry();
        var seen = 0;
        core.For("a.mod").AllowOwner(Stack, "kral-f");
        core.For("a.mod").Subscribe("a.mod:count", 0, _ => seen++);
        core.DependentFailed("a.mod");
        var system = core.Build(Network(core));

        Assert.Equal(["hn-f", "shek-f"], core.Node(Stack)!.AllowedOwners);
        Assert.Equal(CaptureResult.NotAllowed, core.For("x.mod").RequestCapture(Stack, "kral-f"));
        Assert.True(core.IsPenalized(Stack));
        system.Step(100);
        Assert.Equal(0, seen);
    }

    [Fact]
    public void Reads_are_empty_before_a_world_exists()
    {
        var core = new LogisticsCoreEntry();

        Assert.Empty(core.Nodes());
        Assert.Null(core.Node(Stack));
        Assert.False(core.IsPenalized(Stack));
    }
}
