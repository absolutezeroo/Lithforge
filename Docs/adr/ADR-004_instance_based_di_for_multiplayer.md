# ADR-004: Instance-Based Dependency Injection for Multiplayer

## Status
Accepted

## Context
Several core runtime types were implemented as static singletons:

- **FrameProfiler** — static methods, static `Stopwatch[]` arrays, static rolling history
- **PipelineStats** — static per-frame and cumulative counters
- **ToolTemplateRegistry** — static dictionary of tool templates
- **WorldLauncher** — static "mailbox" pattern (set static fields, then load scene)

This pattern blocks multiplayer in three ways:

1. **Multi-world isolation**: A dedicated server hosting multiple worlds cannot have per-world profiling or pipeline counters — statics are process-global.
2. **Headless server builds**: Static profilers and stats still allocate `Stopwatch[]` arrays and history buffers even when no one is observing them. There is no way to substitute a no-op implementation.
3. **Testability**: Unit tests cannot reset or isolate static state between runs without reflection hacks.

The WorldLauncher mailbox pattern is particularly fragile — it relies on setting static fields before a `SceneManager.LoadScene()` call, creating an implicit temporal coupling that is invisible to the type system.

## Decision
Convert all four static singletons to **instance-based types with interface + Null Object pattern**. Wire dependencies via **manual constructor/setter injection in LithforgeBootstrap** — no DI container.

### New Types Created

| Interface | Concrete | Null Object | Tier | Namespace |
|-----------|----------|-------------|------|-----------|
| `IFrameProfiler` | `FrameProfiler` | `NullFrameProfiler` | 3 | `Lithforge.Runtime.Debug` |
| `IPipelineStats` | `PipelineStats` | `NullPipelineStats` | 3 | `Lithforge.Runtime.Debug` |
| — | `WorldSession` | — | 3 | `Lithforge.Runtime.World` |

`FrameProfilerSections` remains a static constants class (section indices and names are process-global by nature).

`WorldSession` is an immutable value object carrying session parameters (WorldPath, DisplayName, Seed, GameMode, IsNewWorld). It replaces the static `WorldLauncher` mailbox — instead of setting statics and loading a scene, callers pass a `WorldSession` instance to `LithforgeBootstrap.RunGameSession()`.

### Wiring Pattern

```
LithforgeBootstrap.Awake()
  ├── new FrameProfiler()        → _frameProfiler
  ├── new PipelineStats()        → _pipelineStats
  └── ...

LithforgeBootstrap.InitializeGameLoop()
  ├── GameLoop.SetFrameProfiler(_frameProfiler)
  ├── GameLoop.SetPipelineStats(_pipelineStats)
  └── ...

GameLoop propagates to:
  ├── MetricsRegistry.Initialize(IFrameProfiler, IPipelineStats)
  ├── F3DebugOverlay(IFrameProfiler, IPipelineStats)
  ├── BenchmarkRunner(IFrameProfiler, IPipelineStats)
  ├── GenerationScheduler(IPipelineStats)
  ├── MeshScheduler(IPipelineStats)
  ├── LODScheduler(IPipelineStats)
  └── MegaMeshBuffer(IPipelineStats)
```

GameLoop defaults to `NullFrameProfiler` and `NullPipelineStats` at construction time. If `SetFrameProfiler()`/`SetPipelineStats()` are never called (e.g., headless server), the null implementations silently discard all calls.

## Rationale

### Why No DI Container (VContainer, Zenject)
- **Scope**: Only 4 types needed conversion. A DI container adds a package dependency, reflection-based registration, and a learning curve for contributors — all for 4 bindings.
- **Burst boundary**: DI containers operate in managed land. Tier 2 Burst jobs cannot participate in container resolution. Manual wiring keeps the injection pattern consistent across tiers.
- **Debuggability**: With manual wiring, every dependency is a visible constructor/setter call in LithforgeBootstrap. Container-based wiring hides the graph behind registration DSLs.
- **Future option**: If the binding count grows past ~15 types, migrating to VContainer is straightforward — the interfaces and null objects already exist. The current manual wiring is a stepping stone, not a dead end.

### Why Null Object (Not Optional/Nullable)
- **Zero null checks on hot paths**: Schedulers call `_stats.IncrGenScheduled()` without `if (_stats != null)`. The null object absorbs the call.
- **Headless server readiness**: A headless server build can use `NullFrameProfiler` + `NullPipelineStats` with zero code changes to consumers. No `#if UNITY_SERVER` conditionals needed.
- **Fail-safe default**: GameLoop constructs with null objects. Even if bootstrap wiring is incomplete, the game runs without NullReferenceExceptions.

### Why WorldSession Replaces WorldLauncher
- **Explicit data flow**: Session parameters are passed as a constructor argument, not read from static fields. The type system enforces that a session exists before the game loop starts.
- **Multi-session capability**: Nothing prevents creating multiple `WorldSession` instances — essential for a server hosting multiple worlds.
- **Immutability**: All properties are read-only. No risk of mid-session mutation from unrelated code.

## Consequences

### Files Modified (~17 consumers)
- `LithforgeBootstrap.cs` — instantiation + wiring
- `GameLoop.cs` — setter injection, null object defaults
- `MetricsRegistry.cs` — receives both interfaces
- `F3DebugOverlay.cs` — receives both interfaces
- `BenchmarkRunner.cs` — receives both interfaces
- `GenerationScheduler.cs` — receives `IPipelineStats`
- `MeshScheduler.cs` — receives `IPipelineStats`
- `LODScheduler.cs` — receives `IPipelineStats`
- `ChunkMeshStore.cs` — receives `IPipelineStats`
- `MegaMeshBuffer.cs` — receives `IPipelineStats`

### Files Created (7 new types)
- `IFrameProfiler.cs`, `NullFrameProfiler.cs`
- `IPipelineStats.cs`, `NullPipelineStats.cs`
- `FrameProfilerSections.cs` (extracted constants)
- `WorldSession.cs`

### What This Unblocks
- **Priority 2 (Service Extraction)**: Services can accept `IFrameProfiler`/`IPipelineStats` via constructor — no static coupling.
- **Priority 3 (Per-Player Physics)**: Per-world `WorldSession` enables isolated physics state per session.
- **Headless server builds**: Null objects eliminate the need for `#if UNITY_SERVER` around every profiling call.
- **Unit testing**: Tests can inject `NullFrameProfiler`/`NullPipelineStats` without resetting static state.
