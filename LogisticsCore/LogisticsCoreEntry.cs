using Logistics.Seed;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Population;

namespace Logistics.Core;

/// <summary>
/// The logistics.core entry (meitou.json "entry") and its API (<see cref="ILogisticsApi"/>, docs/logistics-core.md). Dependents register in
/// their <c>Initialize</c>; the first world built closes registration, and each world gets its own <see cref="LogisticsSystem"/>.
/// </summary>
public sealed class LogisticsCoreEntry : IMod, ILogisticsApi, IModDependents
{
    /// <summary>
    /// The mod's manifest id.
    /// </summary>
    public const string Id = "logistics.core";

    /// <summary>
    /// The dependents' owner changes.
    /// </summary>
    readonly OwnerRules _rules = new();

    /// <summary>
    /// The dependents' event handlers.
    /// </summary>
    readonly Subscriptions _subscriptions = new();

    /// <summary>
    /// The dependents' handles by mod id.
    /// </summary>
    readonly Dictionary<string, ModHandle> _handles = new(StringComparer.Ordinal);

    /// <summary>
    /// Where messages go: the game's mod log once initialized.
    /// </summary>
    Action<string> _log = Console.WriteLine;

    /// <summary>
    /// The running world's system; null before the first world is built.
    /// </summary>
    LogisticsSystem? _current;

    /// <inheritdoc />
    public void Initialize(ModContext context)
    {
        _log = context.Log;
        context.AddSystem("network", Build, SystemAnchor.AfterPart(StandardParts.Population));
    }

    /// <summary>
    /// A world's system: registration closes, the vanilla towns are seeded with the dependents' owner changes.
    /// </summary>
    LogisticsSystem Build(SystemBuildContext context) => Build(NetworkFor(context.Data));

    /// <summary>
    /// The network of a world's population data, at its first hour unknown (0) until the system's first step.
    /// </summary>
    LogisticsNetwork NetworkFor(PopulationData data) => new(LogisticsNetwork.Vanilla(VanillaNodes.Build(data)), _rules, 0, _log);

    /// <summary>
    /// The system of <paramref name="network"/>, which becomes the running world's (also how the tests and the runner build one).
    /// </summary>
    internal LogisticsSystem Build(LogisticsNetwork network)
    {
        _rules.Freeze();
        _subscriptions.Freeze();
        _current = new LogisticsSystem(network, _subscriptions.ForWorld(_log), _log);
        return _current;
    }

    /// <summary>
    /// The owner changes, for building a network outside a world.
    /// </summary>
    internal OwnerRules Rules => _rules;

    /// <inheritdoc />
    public ILogisticsMod For(string modId)
    {
        ArgumentException.ThrowIfNullOrEmpty(modId);
        if (!_handles.TryGetValue(modId, out var handle)) _handles[modId] = handle = new ModHandle(this, modId);
        return handle;
    }

    /// <inheritdoc />
    public IReadOnlyList<NodeInfo> Nodes() => _current?.Network.Nodes() ?? [];

    /// <inheritdoc />
    public NodeInfo? Node(string nodeId) => _current?.Network.Node(nodeId);

    /// <inheritdoc />
    public bool IsPenalized(string nodeId) => _current?.Network.IsPenalized(nodeId) ?? false;

    /// <inheritdoc />
    public void DependentFailed(string modId)
    {
        _rules.Remove(modId);
        _subscriptions.Remove(modId);
        _handles.Remove(modId);
    }

    /// <summary>
    /// One dependent's handle.
    /// </summary>
    sealed class ModHandle(LogisticsCoreEntry core, string modId) : ILogisticsMod
    {
        /// <inheritdoc />
        public string ModId => modId;

        /// <inheritdoc />
        public void AllowOwner(string nodeId, string faction) => core._rules.Add(modId, nodeId, faction, allow: true);

        /// <inheritdoc />
        public void DisallowOwner(string nodeId, string faction) => core._rules.Add(modId, nodeId, faction, allow: false);

        /// <inheritdoc />
        public void Subscribe(string handlerId, int order, Action<LogisticsEvent> handler) =>
            core._subscriptions.Add(modId, handlerId, order, handler);

        /// <inheritdoc />
        public CaptureResult RequestCapture(string nodeId, string faction)
        {
            var network = core._current?.Network ?? throw new InvalidOperationException("No world is running.");
            return network.RequestCapture(nodeId, faction);
        }
    }
}
