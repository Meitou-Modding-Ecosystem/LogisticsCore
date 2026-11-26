# Logistics Core

The logistics framework for [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) mods: nodes, edges, owners, stock and
transports, which War Orchestration and its implementations build on (design: Meitou-Modding-Ecosystem/RivalryCore#59). The `logistics.core`
mod holds the node network: allowed owners, captures, the refused-flip penalty, events and the save ([docs/logistics-core.md](docs/logistics-core.md)).
It seeds nodes from every placed town ([docs/vanilla-nodes.md](docs/vanilla-nodes.md)); vanilla edge seeding finds which places the game's own
roads join, and each trip's cost ([docs/vanilla-edges.md](docs/vanilla-edges.md)). The headless balance runner runs a network without the game
([docs/balance-runner.md](docs/balance-runner.md)).
Owners: @aaallleeeccc and @TheIvoryDisaster. GPL-3.0-or-later.

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).
- Agent rules for the engine pin, versions, CI and releases: [CLAUDE.md](CLAUDE.md) (same as `AGENTS.md`); step-by-step procedures:
  `.claude/skills/meitou-engine-release`.

## Numbers

Task types and dialogue codes are shared by every loaded mod; Logistics Core claims none yet; its block is reserved
([mod number blocks](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/numbers.md)).

Block 4800-4899

| Number | Pool | Use |
| --- | --- | --- |

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. `git pull` does not move it: after
  every pull run `powershell -ExecutionPolicy Bypass -File tools\Update-Local.ps1 -Clean -Build`
  ([keep clones current](docs/local-development.md#keep-clones-current)).
- `LogisticsCore/`: the `logistics.core` mod (entry, network, tick system, save). `LogisticsCore.Contracts/`: its exported contract.
  `LogisticsCore.Tests/`: their tests and the runner's (no game install needed).
- `LogisticsRunner/`: the headless balance runner.
- `LogisticsSeed/`: vanilla node seeding, a library the mod ships privately. `LogisticsSeed.Tests/`: its tests (towns made in memory, no game
  install needed).
- `LogisticsEdges/`: vanilla edge seeding, a library. `LogisticsEdges.Tests/`: its tests (places and roads made in memory, no game install
  needed).
- `Play/`: one-click build, deploy and run.
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`.
