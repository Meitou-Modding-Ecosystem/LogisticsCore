namespace Logistics.Core;

/// <summary>
/// The dependents' changes to nodes' allowed owners (<see cref="ILogisticsMod.AllowOwner"/>, <see cref="ILogisticsMod.DisallowOwner"/>), kept by
/// the mod that made them and applied to each world's seeded nodes. Frozen once the first world is built.
/// </summary>
public sealed class OwnerRules
{
    /// <summary>
    /// One change: <paramref name="Allow"/> adds the faction, else takes it off.
    /// </summary>
    sealed record Rule(string Mod, string NodeId, string Faction, bool Allow);

    /// <summary>
    /// The changes in registration order.
    /// </summary>
    readonly List<Rule> _rules = [];

    /// <summary>
    /// Whether registration has closed.
    /// </summary>
    public bool Frozen { get; private set; }

    /// <summary>
    /// Records <paramref name="mod"/>'s allowing (or disallowing) <paramref name="faction"/> on <paramref name="nodeId"/>.
    /// </summary>
    public void Add(string mod, string nodeId, string faction, bool allow)
    {
        if (Frozen) throw new InvalidOperationException("Logistics Core takes owner changes only in Initialize.");
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentException.ThrowIfNullOrEmpty(faction);

        _rules.Add(new Rule(mod, nodeId, faction, allow));
    }

    /// <summary>
    /// Closes registration.
    /// </summary>
    public void Freeze() => Frozen = true;

    /// <summary>
    /// Drops every change <paramref name="mod"/> made (its <c>Initialize</c> failed).
    /// </summary>
    public void Remove(string mod) => _rules.RemoveAll(r => r.Mod == mod);

    /// <summary>
    /// <paramref name="seeded"/> with the changes for <paramref name="nodeId"/> applied in order, sorted: allowing adds, disallowing removes and is
    /// logged, but never removes <paramref name="holder"/>.
    /// </summary>
    public IReadOnlyList<string> Apply(string nodeId, IEnumerable<string> seeded, string? holder, Action<string> log)
    {
        var owners = new SortedSet<string>(seeded, StringComparer.Ordinal);
        foreach (var rule in _rules.Where(r => r.NodeId == nodeId))
        {
            if (rule.Allow)
            {
                owners.Add(rule.Faction);
                continue;
            }

            if (rule.Faction == holder)
            {
                log($"{rule.Mod} disallowed {rule.Faction} on {nodeId}, which holds it: kept");
                continue;
            }

            if (owners.Remove(rule.Faction)) log($"{rule.Mod} disallowed {rule.Faction} on {nodeId}");
        }

        return [.. owners];
    }

    /// <summary>
    /// The node ids the changes name, for logging the ones the world lacks.
    /// </summary>
    public IEnumerable<string> NodeIds => _rules.Select(r => r.NodeId).Distinct(StringComparer.Ordinal);
}
