# Assembly layout

The multiplayer-rts code is split into three assemblies with a strict one-way
dependency direction, mirroring the layers in the design document:

```
Client  ->  Net  ->  Core
```

- **MultiplayerRts.Core** — pure, framework-independent simulation logic and data
  models. Compiled with `noEngineReferences: true`, so it has **no Unity or Netcode
  dependency** and can be property-tested in isolation against in-memory state.
  Geometry uses the project's own `MapVector` rather than `UnityEngine.Vector2` to
  keep this boundary clean.
- **MultiplayerRts.Net** — Netcode for GameObjects integration: `NetworkVariable`
  replication, RPCs, lobby/transport wiring. References Core only.
- **MultiplayerRts.Client** — thin presentation layer: input, placeholder rendering,
  replicated-state mirroring. References Net and Core.

Nothing references Client, and Core references nothing, so the canonical simulation
never depends on networking or presentation.

## Netcode dependency and package restore

The `MultiplayerRts.Net` assembly references the Netcode for GameObjects runtime
(`Unity.Netcode.Runtime`), declared in `Packages/manifest.json`
(`com.unity.netcode.gameobjects` + `com.unity.transport`). The Netcode-touching
files under `Net/Replication` (`MatchReplicationBehaviour.cs`, `NetRecords.cs`)
compile **only after a Unity package restore** brings that package into the project
(open the project in the Unity Editor, or run a Package Manager resolve, so the NGO
assemblies are available to the compiler).

The replication design is split so the bulk is verifiable without Netcode:

- `Net/Replication/MatchStateSnapshot.cs` and `MatchStateProjector.cs` are **pure**
  (reference only `MultiplayerRts.Core`, no Netcode). They map `MatchState` to an
  immutable snapshot and detect change via `MatchState.Revision`, and can be compiled
  and tested out-of-tree with `dotnet`.
- `MatchReplicationBehaviour.cs` and `NetRecords.cs` are the thin Netcode layer: the
  `NetworkBehaviour` owns the `NetworkVariable`/`NetworkList` fields and assigns them
  from the pure snapshot whenever the projector reports a change. These require the
  package restore above to compile in-editor.
