namespace Logistics;

/// <summary>
/// Logistics Core's entry as dependents see it: <c>context.Dependency&lt;ILogisticsApi&gt;("logistics.core")</c> in <c>Initialize</c>
/// (docs/logistics-core.md). Reads answer for the running world; before a world exists they are empty.
/// </summary>
public interface ILogisticsApi
{
    /// <summary>
    /// The handle a dependent registers through, named by its own manifest id (<c>context.Id</c>). Every id it registers starts with
    /// <c>&lt;modId&gt;:</c>. Asking twice gives the same handle.
    /// </summary>
    ILogisticsMod For(string modId);

    /// <summary>
    /// Every node of the running world, sorted by id; empty before a world exists.
    /// </summary>
    IReadOnlyList<NodeInfo> Nodes();

    /// <summary>
    /// The node with <paramref name="nodeId"/> in the running world; null when there is none.
    /// </summary>
    NodeInfo? Node(string nodeId);

    /// <summary>
    /// Whether the node is under the refused-flip penalty now (<see cref="NodeInfo.PenaltyUntilHour"/>): it spawns no new squads but its leader
    /// and bodyguards, and its passive yields that need no worker squad are off.
    /// </summary>
    bool IsPenalized(string nodeId);
}

/// <summary>
/// One dependent's registrations with Logistics Core. The registering members are for <c>Initialize</c> only (they throw once the first world
/// is built); <see cref="RequestCapture"/> is for a serial phase of a running world.
/// </summary>
public interface ILogisticsMod
{
    /// <summary>
    /// The dependent's manifest id.
    /// </summary>
    string ModId { get; }

    /// <summary>
    /// Adds <paramref name="faction"/> (a FACTION record id) to the node's allowed owners. Union across mods, idempotent. A node the world does
    /// not have is logged and ignored when the world is built.
    /// </summary>
    void AllowOwner(string nodeId, string faction);

    /// <summary>
    /// Takes <paramref name="faction"/> off the node's allowed owners, logged. It never removes the node's current holder.
    /// </summary>
    void DisallowOwner(string nodeId, string faction);

    /// <summary>
    /// Subscribes <paramref name="handler"/> to every <see cref="LogisticsEvent"/>. <paramref name="handlerId"/> is <c>&lt;modId&gt;:&lt;name&gt;</c>
    /// and unique; handlers run in (<paramref name="order"/>, <paramref name="handlerId"/>) order in Logistics Core's serial step and may only
    /// queue commands. One that throws is logged and dropped for that world.
    /// </summary>
    void Subscribe(string handlerId, int order, Action<LogisticsEvent> handler);

    /// <summary>
    /// Asks for the node to change hands to <paramref name="faction"/>. Refused when the faction is not an allowed owner, which puts the node under
    /// the refused-flip penalty for 24 game hours (docs/logistics-core.md "Captures"). Serial phases only.
    /// </summary>
    CaptureResult RequestCapture(string nodeId, string faction);
}
