# Balance runner

`LogisticsRunner/` runs a Logistics Core network hour by hour without the game and prints each day's numbers and the final state hash. It is the
harness for Meitou-Modding-Ecosystem/RivalryCore#59's balance checks.

```powershell
dotnet run --project LogisticsRunner -- --days 30 --seed 1 --save-at 200
```

| Option | Default | Meaning |
| --- | --- | --- |
| `--days` | 30 | game days to run |
| `--towns`, `--nests` | 40, 8 | size of the synthetic map |
| `--seed` | 1 | seed of the map and the policy's rolls |
| `--save-at` | none | hour (from the start) at which the network is saved and loaded into a fresh one; the run is compared with one without, and exits 1 when the hashes differ |

Each day prints towns held per faction, nodes under the refused-flip penalty, and the day's events by kind.

## Map and policy

- The map is synthetic (`SyntheticMap`): towns spread over five factions, each allowing its holder and one or two rivals, plus destroyable nests.
  The same seed gives the same map. The real map (vanilla nodes, deposits and production tables) is not seeded yet.
- An `IPolicy` drives the network each hour in place of War Orchestration's rules. The default, `RandomCaptures`, has a random faction try to
  take a random town one hour in six.
- Runs start at game hour 37 (day 1, 13:00, as a new game). Rolls come from the engine's `Rng`, so a run is the same on every machine.

`LogisticsCore.Tests` (`SaveTests`) checks that a save and load mid-run gives the same hash, and that the same seed gives the same run.
