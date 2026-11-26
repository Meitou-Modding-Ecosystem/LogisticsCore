# Logistics Core (the mod)

The `logistics.core` mod (folder `LogisticsCore/`, Meitou-Modding-Ecosystem/LogisticsCore#32): the node network that War Orchestration and its
implementations build on. Neutral machinery: nodes, their allowed owners, captures, the refused-flip penalty, events and the save. Stock,
deposits, facilities, squads and transports come with Meitou-Modding-Ecosystem/RivalryCore#106-#109 and #117.

## Assemblies

| Assembly | What | Exported |
| --- | --- | --- |
| `LogisticsCore.Contracts.dll` | `ILogisticsApi`, `ILogisticsMod`, `NodeInfo`, `NodeKind`, `NodeState`, `CaptureResult`, the events. No engine types | yes (the versioned contract) |
| `LogisticsCore.dll` | the entry `Logistics.Core.LogisticsCoreEntry`, the network, the tick system, the save | yes (the entry) |
| `LogisticsSeed.dll` | vanilla node seeding ([vanilla-nodes.md](vanilla-nodes.md)) | no (private) |

A breaking change to `LogisticsCore.Contracts` is a major version bump.

## Using Logistics Core

```csharp
// meitou.json: "dependencies": { "logistics.core": ">=0.1.0 <0.2.0" }; reference LogisticsCore.Contracts with Private="false".
public void Initialize(ModContext c)
{
    var logistics = c.Dependency<ILogisticsApi>("logistics.core")!.For(c.Id);
    logistics.AllowOwner("town:9-rebirth-INGAME", "my-faction-id");
    logistics.Subscribe($"{c.Id}:captures", order: 0, e => { if (e is OwnerChanged o) Queue(o); });
}
```

- `For(modId)` gives the dependent's handle. Ids it registers are `<modId>:<name>`; a wrong prefix or a duplicate throws.
- `AllowOwner`, `DisallowOwner` and `Subscribe` are for `Initialize` only: the first world built closes registration and they throw after.
  A dependent whose `Initialize` fails loses all three (`IModDependents.DependentFailed`).
- `RequestCapture` is for a serial phase of a running world; it throws when no world runs. `Nodes()`, `Node(id)` and `IsPenalized(id)` read the
  running world and are empty before one exists.

## Nodes and owners

- Every placed town with an instance id is a node `town:<InstanceId>` ([vanilla-nodes.md](vanilla-nodes.md)). Towns are `Ownable`; nests, nest
  markers, points of interest and Null towns are `Destroyable`. State: `Standing`, `Cleared` or `Ruined`.
- Allowed owners: the town's own versions' factions, plus its current holder, then each dependent's `AllowOwner` (union, idempotent) and
  `DisallowOwner` (logged; never removes the current holder) in registration order. A change for a node the world lacks is logged and ignored.
- Invariant: a node's owner is always one of its allowed owners (or none).

## Captures

`RequestCapture(nodeId, faction)` answers:

| Answer | When | Effect |
| --- | --- | --- |
| `Captured` | the faction is allowed | owner changes, a cleared node stands again; `OwnerChanged` |
| `AlreadyOwner` | the faction holds it | nothing |
| `NotAllowed` | the faction is not an allowed owner | owner kept; `CaptureRefused`; the refused-flip penalty starts (`PenaltyStarted`) |
| `NotOwnable` | a destroyable or ruined node | `CaptureRefused`, no penalty |
| `UnknownNode` | no such node | nothing |

### Refused-flip penalty

A town that should flip but can't (its capture is refused as `NotAllowed`) is still penalized, for **24 game hours**:

- it spawns no new squads except its leader and the leader's bodyguards;
- any passive resource generation it has that needs no worker squad is disabled.

A refusal during the penalty restarts the 24 hours. `IsPenalized(nodeId)` and `NodeInfo.PenaltyUntilHour` (whole game hours, as
`World.GameHours`) tell spawners and producers (Logistics Core's own squads and facilities once they exist, War Orchestration and its
implementations) to hold back; `PenaltyEnded` is raised when it runs out.

## Events

`OwnerChanged`, `CaptureRefused`, `PenaltyStarted`, `PenaltyEnded` (each with the game hour and node id). Queued when they happen and delivered
in Logistics Core's serial step, in queue order, to every handler in (`order`, handler id) order. Handlers may only queue commands. One that throws
is logged and dropped for that world.

## Tick

One system, `logistics.core/network`, after the population (`SystemAnchor.AfterPart(StandardParts.Population)`). `IWorldReady` sets its clock to
the world's game hour; each slow-world step moves the clock to the whole game hour (ending penalties) and delivers the queued events. Its state
(clock, every node's owner, state and penalty, the queued events) enters `World.StateHash` (`IStateHashed`).

## Save

An `IModSaveData` sidecar, JSON: `schema` (1), `hour`, `nodes` (`id`, `owner`, `state`, `penaltyUntil`) and the queued `events`.

- A saved node the world lacks (its mod is gone) is kept verbatim and written with the next save.
- A saved owner that is no longer allowed (the dependent that allowed it is gone) reverts to the node's seeded owner, logged.
- A schema newer than this build reads leaves the network as seeded, logged.

Balance and determinism runs without the game: [balance-runner.md](balance-runner.md).
