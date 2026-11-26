namespace Logistics.Core;

/// <summary>
/// The dependents' event handlers (<see cref="ILogisticsMod.Subscribe"/>), delivered in (order, handler id) order. Frozen once the first world is
/// built; a handler that throws is logged and dropped for the world it threw in (each world delivers through its own <see cref="Delivery"/>).
/// </summary>
public sealed class Subscriptions
{
    /// <summary>
    /// One handler.
    /// </summary>
    public sealed record Handler(string Mod, string Id, int Order, Action<LogisticsEvent> Run);

    /// <summary>
    /// The handlers, sorted by (order, id).
    /// </summary>
    readonly List<Handler> _handlers = [];

    /// <summary>
    /// Whether registration has closed.
    /// </summary>
    public bool Frozen { get; private set; }

    /// <summary>
    /// Adds a handler; its id must start with <c>&lt;mod&gt;:</c> and be unique.
    /// </summary>
    public void Add(string mod, string handlerId, int order, Action<LogisticsEvent> run)
    {
        if (Frozen) throw new InvalidOperationException("Logistics Core takes subscriptions only in Initialize.");
        ArgumentNullException.ThrowIfNull(run);
        Ids.Check(mod, handlerId);
        if (_handlers.Any(h => h.Id == handlerId)) throw new ArgumentException($"Handler {handlerId} is already subscribed.", nameof(handlerId));

        _handlers.Add(new Handler(mod, handlerId, order, run));
        _handlers.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
    }

    /// <summary>
    /// Closes registration.
    /// </summary>
    public void Freeze() => Frozen = true;

    /// <summary>
    /// Drops <paramref name="mod"/>'s handlers (its <c>Initialize</c> failed).
    /// </summary>
    public void Remove(string mod) => _handlers.RemoveAll(h => h.Mod == mod);

    /// <summary>
    /// A world's delivery: the handlers as they are now, each dropped from it alone when it throws.
    /// </summary>
    public Delivery ForWorld(Action<string> log) => new([.. _handlers], log);

    /// <summary>
    /// One world's handlers.
    /// </summary>
    public sealed class Delivery(List<Handler> handlers, Action<string> log)
    {
        /// <summary>
        /// The handler ids still subscribed in this world, in delivery order.
        /// </summary>
        public IEnumerable<string> Ids => handlers.Select(h => h.Id);

        /// <summary>
        /// Hands every event, in queue order, to every handler in turn.
        /// </summary>
        public void Deliver(IReadOnlyList<LogisticsEvent> events)
        {
            foreach (var e in events)
                foreach (var handler in handlers.ToArray())
                    Run(handler, e);
        }

        /// <summary>
        /// Runs one handler, dropping it when it throws.
        /// </summary>
        void Run(Handler handler, LogisticsEvent e)
        {
            try
            {
                handler.Run(e);
            }
            catch (Exception ex)
            {
                handlers.Remove(handler);
                log($"handler {handler.Id} threw on {e.GetType().Name} and was dropped: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Registered ids: <c>&lt;modId&gt;:&lt;name&gt;</c>.
/// </summary>
public static class Ids
{
    /// <summary>
    /// Throws unless <paramref name="id"/> is <paramref name="mod"/>, a colon and a non-empty name.
    /// </summary>
    public static void Check(string mod, string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        if (!id.StartsWith(mod + ":", StringComparison.Ordinal) || id.Length == mod.Length + 1)
            throw new ArgumentException($"Id {id} must be {mod}:<name>.", nameof(id));
    }
}
