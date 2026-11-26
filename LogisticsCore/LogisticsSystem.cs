using System.Text.Json.Nodes;
using Meitou.Modding;
using Meitou.Simulation;

namespace Logistics.Core;

/// <summary>
/// Logistics Core's tick system, after the population (docs/logistics-core.md "Tick"): each slow-world step moves the network's clock to the
/// world's whole game hour (ending penalties) and delivers the queued events to the dependents' handlers. Hashed and saved.
/// </summary>
public sealed class LogisticsSystem(LogisticsNetwork network, Subscriptions.Delivery delivery, Action<string> log)
    : IWorldReady, IStateHashed, IModSaveData
{
    /// <summary>
    /// Saved nodes this world lacks, written again with the next save.
    /// </summary>
    List<JsonObject> _kept = [];

    /// <summary>
    /// The world's network.
    /// </summary>
    public LogisticsNetwork Network { get; } = network;

    /// <summary>
    /// The whole game hour of <paramref name="world"/>.
    /// </summary>
    public static long HourOf(World world) => (long)Math.Floor(world.GameHours);

    /// <summary>
    /// Sets the clock to the world's hour before the first tick, so a capture in the first tick counts its penalty from the world's time.
    /// </summary>
    public void Ready(World world, bool fromSave) => Network.AdvanceTo(HourOf(world));

    /// <inheritdoc />
    public void SlowWorld(World world) => Step(HourOf(world));

    /// <summary>
    /// One step at <paramref name="hour"/>: the clock moves, then the queued events are delivered.
    /// </summary>
    public void Step(long hour)
    {
        Network.AdvanceTo(hour);
        var events = Network.TakeEvents();
        if (events.Count > 0) delivery.Deliver(events);
    }

    /// <inheritdoc />
    public void Hash(ref StateHasher hasher) => Network.Hash(ref hasher);

    /// <inheritdoc />
    public void Write(Stream stream) => NetworkSave.Write(Network, _kept, stream);

    /// <inheritdoc />
    public void Read(Stream stream) => _kept = NetworkSave.Read(Network, stream, log);
}
