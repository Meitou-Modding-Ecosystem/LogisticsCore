using System.Text.Json.Serialization;

namespace Logistics;

/// <summary>
/// Something that happened in the network, queued when it happens and delivered in Logistics Core's serial step (docs/logistics-core.md "Events").
/// <see cref="Hour"/> is the game hour it happened in. Events still queued when the game saves are saved with it.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "event")]
[JsonDerivedType(typeof(OwnerChanged), "owner-changed")]
[JsonDerivedType(typeof(CaptureRefused), "capture-refused")]
[JsonDerivedType(typeof(PenaltyStarted), "penalty-started")]
[JsonDerivedType(typeof(PenaltyEnded), "penalty-ended")]
public abstract record LogisticsEvent(long Hour, string NodeId);

/// <summary>
/// The node changed hands from <paramref name="From"/> to <paramref name="To"/>.
/// </summary>
public sealed record OwnerChanged(long Hour, string NodeId, string? From, string? To) : LogisticsEvent(Hour, NodeId);

/// <summary>
/// A capture by <paramref name="Faction"/> was refused (<paramref name="Reason"/>).
/// </summary>
public sealed record CaptureRefused(long Hour, string NodeId, string Faction, CaptureResult Reason) : LogisticsEvent(Hour, NodeId);

/// <summary>
/// The node came under the refused-flip penalty until <paramref name="UntilHour"/> (a refusal while it is under one restarts the clock).
/// </summary>
public sealed record PenaltyStarted(long Hour, string NodeId, long UntilHour) : LogisticsEvent(Hour, NodeId);

/// <summary>
/// The node's refused-flip penalty ran out.
/// </summary>
public sealed record PenaltyEnded(long Hour, string NodeId) : LogisticsEvent(Hour, NodeId);
