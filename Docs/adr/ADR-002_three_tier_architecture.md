# ADR-002: Three-Tier Architecture

## Status
Accepted

## Context
Lithforge needs clean separation between game logic (definitions, registries, content loading), high-performance computation (Burst jobs, NativeContainers), and Unity runtime integration (rendering, input, UI). Mixing these concerns creates untestable, tightly-coupled code.

## Decision
Adopt a three-tier architecture enforced at the assembly level via Unity `.asmdef` files:

- **Tier 1 — Pure C#**: Zero Unity dependencies. Standard .NET types only. Contains definitions, registries, content loading, serialization, validation, events, logging.
- **Tier 2 — Unity Core**: Uses Unity.Collections, Unity.Mathematics, Unity.Burst, Unity.Jobs. NO UnityEngine references. Contains NativeArray chunk data, Burst-compiled jobs, NativeStateRegistry.
- **Tier 3 — Unity Runtime**: Uses UnityEngine, URP, InputSystem, UI Toolkit. Contains MonoBehaviours, mesh upload, shaders, input handling, audio, debug overlays.

## Rationale

### Why Three Tiers (Not Two)
- **Tier 1 isolation**: Content loading, validation, and registry logic can be unit-tested without Unity. Could theoretically compile against .NET Standard outside Unity.
- **Tier 2 isolation**: Burst jobs and NativeContainers need Unity.Collections/Mathematics/Burst but NOT UnityEngine. This prevents accidental main-thread-only API calls in job code.
- **Tier 3 as thin layer**: Mesh upload, UI, and input are inherently Unity-specific. Keeping them in a separate tier prevents leaking UnityEngine dependencies into game logic.

### Enforcement
- Each tier has its own `.asmdef` with `noEngineReferences: true` (Tiers 1 and 2).
- Dependency direction is strictly downward: Tier 3 → Tier 2 → Tier 1.
- Violations are compile-time errors, not runtime checks.

### Dual Representation Pattern
Types that need both managed (Tier 1, for content loading and modding) and Burst-compatible (Tier 2, for jobs) forms use a `BakeNative()` method to produce the blittable version. The managed version is authoritative; the native version is derived and must match 1:1.

## Consequences
- Some types (like BlockDefinition) conceptually belong to "voxel" but must live in or be accessible from Tier 1. This is solved by placing them in the Tier 2 package but without Unity type usage in the file itself.
- Content loading code in Tier 1 cannot use Unity's JsonUtility. Newtonsoft.Json (pure .NET) is used instead, accessed via `precompiledReferences` in the asmdef.
- More `.asmdef` files to maintain, but the compile-time boundary enforcement is worth the overhead.
