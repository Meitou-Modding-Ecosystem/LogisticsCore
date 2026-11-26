using Logistics.Seed;
using Meitou.Simulation;

namespace Logistics.Core;

/// <summary>
/// A node as the network is seeded with it: its id, name, kind, holder and the owners its own data allows (before the dependents' changes).
/// </summary>
public sealed record NodeSeed(string Id, string Name, NodeKind Kind, string? Owner, IReadOnlyList<string> AllowedOwners);

/// <summary>
/// One world's node network (docs/logistics-core.md): nodes, their owners and states, captures, the refused-flip penalty and the event queue.
/// Pure state: the world reaches it through <see cref="LogisticsSystem"/>, the headless runner directly. Changed only in serial phases.
/// </summary>
public sealed class LogisticsNetwork
{
    #region Nodes

    /// <summary>
    /// How long a refused flip penalizes a node, in game hours.
    /// </summary>
    public const long PenaltyHours = 24;

    /// <summary>
    /// The nodes by id, sorted.
    /// </summary>
    readonly SortedDictionary<string, NodeRecord> _nodes = new(StringComparer.Ordinal);

    /// <summary>
    /// The game hour the network has advanced to.
    /// </summary>
    public long Hour { get; private set; }

    /// <summary>
    /// A network of <paramref name="seeds"/> with <paramref name="rules"/> applied, at <paramref name="hour"/>. A seed whose holder is not allowed
    /// after the rules keeps it (its holder joins the list: the invariant holds from the start); a duplicate id is refused.
    /// </summary>
    public LogisticsNetwork(IEnumerable<NodeSeed> seeds, OwnerRules rules, long hour, Action<string> log)
    {
        Hour = hour;
        foreach (var seed in seeds)
        {
            if (_nodes.ContainsKey(seed.Id)) throw new ArgumentException($"Node {seed.Id} is seeded twice.", nameof(seeds));

            var allowed = rules.Apply(seed.Id, seed.Owner is null ? seed.AllowedOwners : [.. seed.AllowedOwners, seed.Owner], seed.Owner, log);
            _nodes.Add(seed.Id, new NodeRecord(seed, allowed));
        }

        foreach (string missing in rules.NodeIds.Where(id => !_nodes.ContainsKey(id)))
            log($"owner changes for {missing} ignored: the world has no such node");
    }

    /// <summary>
    /// The seeds of the vanilla towns (<see cref="VanillaNodes"/>): ownable towns, and nests, markers and points of interest as destroyable.
    /// </summary>
    public static IEnumerable<NodeSeed> Vanilla(IEnumerable<VanillaNode> nodes) =>
        nodes.Select(n => new NodeSeed(n.Id, n.Name, n.Ownable ? NodeKind.Ownable : NodeKind.Destroyable, n.Owner, n.AllowedOwners));

    /// <summary>
    /// Every node as dependents read it, sorted by id.
    /// </summary>
    public IReadOnlyList<NodeInfo> Nodes() => [.. _nodes.Values.Select(n => n.Info())];

    /// <summary>
    /// The node with <paramref name="nodeId"/>; null when there is none.
    /// </summary>
    public NodeInfo? Node(string nodeId) => _nodes.TryGetValue(nodeId, out var n) ? n.Info() : null;

    /// <summary>
    /// Whether the node is under the refused-flip penalty.
    /// </summary>
    public bool IsPenalized(string nodeId) => _nodes.TryGetValue(nodeId, out var n) && n.PenaltyUntil is not null;

    /// <summary>
    /// The mutable node.
    /// </summary>
    internal bool TryGet(string nodeId, out NodeRecord node) => _nodes.TryGetValue(nodeId, out node!);

    /// <summary>
    /// The nodes in id order.
    /// </summary>
    internal IEnumerable<NodeRecord> All => _nodes.Values;

    /// <summary>
    /// A node's state.
    /// </summary>
    internal sealed class NodeRecord(NodeSeed seed, IReadOnlyList<string> allowed)
    {
        /// <summary>
        /// What it was seeded with.
        /// </summary>
        public NodeSeed Seed { get; } = seed;

        /// <summary>
        /// Its allowed owners after the dependents' changes, sorted.
        /// </summary>
        public IReadOnlyList<string> Allowed { get; } = allowed;

        /// <summary>
        /// Its holder.
        /// </summary>
        public string? Owner { get; set; } = seed.Owner;

        /// <summary>
        /// Its state.
        /// </summary>
        public NodeState State { get; set; }

        /// <summary>
        /// The hour its refused-flip penalty ends; null without one.
        /// </summary>
        public long? PenaltyUntil { get; set; }

        /// <summary>
        /// The node as dependents read it.
        /// </summary>
        public NodeInfo Info() => new(Seed.Id, Seed.Name, Seed.Kind, State, Owner, Allowed, PenaltyUntil);
    }

    #endregion

    #region Captures

    /// <summary>
    /// Asks for <paramref name="nodeId"/> to pass to <paramref name="faction"/>: done and <see cref="OwnerChanged"/> queued when the faction is
    /// allowed; refused (<see cref="CaptureRefused"/>) otherwise. A refusal because the faction is not allowed is a flip that should happen but
    /// can't, so the node comes under the refused-flip penalty for <see cref="PenaltyHours"/> (<see cref="PenaltyStarted"/>).
    /// </summary>
    public CaptureResult RequestCapture(string nodeId, string faction)
    {
        ArgumentException.ThrowIfNullOrEmpty(faction);
        if (!_nodes.TryGetValue(nodeId, out var node)) return CaptureResult.UnknownNode;

        var result = Judge(node, faction);
        if (result == CaptureResult.Captured) Capture(node, faction);
        else if (result != CaptureResult.AlreadyOwner) _events.Add(new CaptureRefused(Hour, nodeId, faction, result));

        if (result == CaptureResult.NotAllowed) Penalize(node);
        return result;
    }

    /// <summary>
    /// What a capture of <paramref name="node"/> by <paramref name="faction"/> comes to.
    /// </summary>
    static CaptureResult Judge(NodeRecord node, string faction)
    {
        if (node.Seed.Kind == NodeKind.Destroyable || node.State == NodeState.Ruined) return CaptureResult.NotOwnable;
        if (node.Owner == faction) return CaptureResult.AlreadyOwner;
        return node.Allowed.Contains(faction, StringComparer.Ordinal) ? CaptureResult.Captured : CaptureResult.NotAllowed;
    }

    /// <summary>
    /// Hands <paramref name="node"/> to <paramref name="faction"/>; a cleared node is standing again.
    /// </summary>
    void Capture(NodeRecord node, string faction)
    {
        string? from = node.Owner;
        node.Owner = faction;
        node.State = NodeState.Standing;
        _events.Add(new OwnerChanged(Hour, node.Seed.Id, from, faction));
    }

    #endregion

    #region Penalty

    /// <summary>
    /// Puts <paramref name="node"/> under the refused-flip penalty for <see cref="PenaltyHours"/> from now (a running one restarts).
    /// </summary>
    void Penalize(NodeRecord node)
    {
        node.PenaltyUntil = Hour + PenaltyHours;
        _events.Add(new PenaltyStarted(Hour, node.Seed.Id, node.PenaltyUntil.Value));
    }

    /// <summary>
    /// Moves the network's clock to <paramref name="hour"/>: penalties ending at or before it end (<see cref="PenaltyEnded"/>). An earlier hour
    /// changes nothing.
    /// </summary>
    public void AdvanceTo(long hour)
    {
        if (hour <= Hour) return;

        Hour = hour;
        foreach (var node in _nodes.Values)
        {
            if (node.PenaltyUntil is not { } until || until > hour) continue;

            node.PenaltyUntil = null;
            _events.Add(new PenaltyEnded(hour, node.Seed.Id));
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Events queued since the last delivery, in the order they happened.
    /// </summary>
    readonly List<LogisticsEvent> _events = [];

    /// <summary>
    /// The queued events.
    /// </summary>
    public IReadOnlyList<LogisticsEvent> Pending => _events;

    /// <summary>
    /// The queued events, emptying the queue.
    /// </summary>
    public IReadOnlyList<LogisticsEvent> TakeEvents()
    {
        if (_events.Count == 0) return [];

        var taken = _events.ToArray();
        _events.Clear();
        return taken;
    }

    /// <summary>
    /// Puts saved events back in the queue (load).
    /// </summary>
    internal void Requeue(IEnumerable<LogisticsEvent> events) => _events.AddRange(events);

    /// <summary>
    /// Sets the clock without ending penalties (load).
    /// </summary>
    internal void SetHour(long hour) => Hour = hour;

    #endregion

    #region Hash

    /// <summary>
    /// Adds the network's state to a world's state hash: the clock, every node's holder, state and penalty, and the queued events.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(Hour);
        foreach (var node in _nodes.Values)
        {
            hasher.Add(Rng.StableHash(node.Seed.Id));
            hasher.Add(node.Owner is null ? 0UL : Rng.StableHash(node.Owner));
            hasher.Add((int)node.State);
            hasher.Add(node.PenaltyUntil ?? -1);
        }

        hasher.Add(_events.Count);
        foreach (var e in _events) hasher.Add(Rng.StableHash(e.ToString()));
    }

    #endregion
}
