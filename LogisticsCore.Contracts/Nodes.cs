namespace Logistics;

/// <summary>
/// What a node is: a place that can be held, or one that can only be cleared (nests, hideouts, points of interest).
/// </summary>
public enum NodeKind
{
    /// <summary>
    /// A place held by an owner that can change hands (towns, outposts).
    /// </summary>
    Ownable,

    /// <summary>
    /// A place that refuses every capture: it can be cleared or ruined, not taken.
    /// </summary>
    Destroyable,
}

/// <summary>
/// A node's state.
/// </summary>
public enum NodeState
{
    /// <summary>
    /// Standing, held by its owner (or by no one).
    /// </summary>
    Standing,

    /// <summary>
    /// Emptied of its holders; it can be reoccupied.
    /// </summary>
    Cleared,

    /// <summary>
    /// Destroyed; it refuses every capture.
    /// </summary>
    Ruined,
}

/// <summary>
/// A node as dependents read it. <see cref="Id"/> is <c>town:&lt;InstanceId&gt;</c> for a vanilla town, <c>&lt;modId&gt;:&lt;name&gt;</c> for a
/// mod's node. <see cref="Owner"/> is always one of <see cref="AllowedOwners"/> (or null). <see cref="PenaltyUntilHour"/> is the game hour
/// (<c>World.GameHours</c>, whole hours) the refused-flip penalty ends; null without one.
/// </summary>
public sealed record NodeInfo(string Id, string Name, NodeKind Kind, NodeState State, string? Owner, IReadOnlyList<string> AllowedOwners,
    long? PenaltyUntilHour);

/// <summary>
/// The answer to <see cref="ILogisticsMod.RequestCapture"/>.
/// </summary>
public enum CaptureResult
{
    /// <summary>
    /// The node changed hands.
    /// </summary>
    Captured,

    /// <summary>
    /// The faction holds it already; nothing changed.
    /// </summary>
    AlreadyOwner,

    /// <summary>
    /// The faction is not an allowed owner: refused, and the node is put under the refused-flip penalty.
    /// </summary>
    NotAllowed,

    /// <summary>
    /// A destroyable or ruined node refuses every capture.
    /// </summary>
    NotOwnable,

    /// <summary>
    /// No node with that id.
    /// </summary>
    UnknownNode,
}
