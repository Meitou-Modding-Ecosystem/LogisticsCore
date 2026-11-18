# Logistics Core

The logistics framework for [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) mods: nodes, edges, owners, stock and
transports, which War Orchestration and its implementations build on (design: Meitou-Modding-Ecosystem/RivalryCore#59). So far it holds
vanilla node seeding: every placed town as a node with its id, owner and possible owners ([docs/vanilla-nodes.md](docs/vanilla-nodes.md)).
Owners: @aaallleeeccc and @TheIvoryDisaster. GPL-3.0-or-later.

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. After a pull that moves it:
  `git submodule update --init`.
- `LogisticsSeed/`: vanilla node seeding, a library (no `meitou.json` yet). `LogisticsSeed.Tests/`: its tests (towns made in memory, no game
  install needed).
- `Play/`: one-click build, deploy and run.
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`.
