using System.Text.Json;
using System.Text.Json.Nodes;

namespace Logistics.Core;

/// <summary>
/// The network's save sidecar (docs/logistics-core.md "Save"): JSON with a schema version, the clock, each node's holder, state and penalty, and
/// the queued events. A saved node the world lacks (its mod is gone) is kept verbatim and written again; a saved holder that is no longer allowed
/// (the dependent that allowed it is gone) reverts to the node's seeded holder, logged.
/// </summary>
public static class NetworkSave
{
    /// <summary>
    /// The schema this build writes; a newer one is refused (the network starts as seeded, logged).
    /// </summary>
    public const int Schema = 1;

    /// <summary>
    /// How events are written.
    /// </summary>
    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>
    /// Writes <paramref name="network"/> (and the nodes kept from the save it was loaded from) to <paramref name="stream"/>.
    /// </summary>
    public static void Write(LogisticsNetwork network, IReadOnlyList<JsonObject> kept, Stream stream)
    {
        var nodes = new JsonArray();
        foreach (var node in network.All) nodes.Add(NodeJson(node));
        foreach (var k in kept) nodes.Add(k.DeepClone());

        var root = new JsonObject
        {
            ["schema"] = Schema,
            ["hour"] = network.Hour,
            ["nodes"] = nodes,
            ["events"] = JsonSerializer.SerializeToNode(network.Pending.ToArray(), Options),
        };
        using var writer = new Utf8JsonWriter(stream);
        root.WriteTo(writer);
    }

    /// <summary>
    /// One node's saved state.
    /// </summary>
    static JsonObject NodeJson(LogisticsNetwork.NodeRecord node) => new()
    {
        ["id"] = node.Seed.Id,
        ["owner"] = node.Owner,
        ["state"] = node.State.ToString(),
        ["penaltyUntil"] = node.PenaltyUntil,
    };

    /// <summary>
    /// Reads a save into <paramref name="network"/>; returns the saved nodes the network lacks, to keep. Bad entries are logged and skipped.
    /// </summary>
    public static List<JsonObject> Read(LogisticsNetwork network, Stream stream, Action<string> log)
    {
        var root = JsonNode.Parse(stream)?.AsObject() ?? throw new InvalidDataException("Empty logistics save.");
        int schema = root["schema"]?.GetValue<int>() ?? 0;
        if (schema is < 1 or > Schema)
        {
            log($"save schema {schema} is not one this build reads (1 to {Schema}): the network starts as seeded");
            return [];
        }

        network.SetHour(root["hour"]?.GetValue<long>() ?? network.Hour);
        var kept = ReadNodes(network, root["nodes"]?.AsArray() ?? [], log);
        if (root["events"] is { } events) network.Requeue(events.Deserialize<LogisticsEvent[]>(Options) ?? []);
        return kept;
    }

    /// <summary>
    /// Restores each saved node the network has; returns the others.
    /// </summary>
    static List<JsonObject> ReadNodes(LogisticsNetwork network, JsonArray nodes, Action<string> log)
    {
        var kept = new List<JsonObject>();
        foreach (var entry in nodes.OfType<JsonObject>())
        {
            string? id = entry["id"]?.GetValue<string>();
            if (id is null) continue;

            if (network.TryGet(id, out var node)) Restore(node, entry, log);
            else kept.Add((JsonObject)entry.DeepClone());
        }

        return kept;
    }

    /// <summary>
    /// Puts one saved node's holder, state and penalty back.
    /// </summary>
    static void Restore(LogisticsNetwork.NodeRecord node, JsonObject entry, Action<string> log)
    {
        string? owner = entry["owner"]?.GetValue<string>();
        if (owner is not null && !node.Allowed.Contains(owner, StringComparer.Ordinal))
        {
            log($"{node.Seed.Id}: saved holder {owner} is no longer allowed, back to {node.Seed.Owner ?? "no one"}");
            owner = node.Seed.Owner;
        }

        node.Owner = owner;
        node.State = Enum.TryParse<NodeState>(entry["state"]?.GetValue<string>(), out var state) ? state : NodeState.Standing;
        node.PenaltyUntil = entry["penaltyUntil"]?.GetValue<long>();
    }
}
