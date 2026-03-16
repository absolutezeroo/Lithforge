# Lithforge — Multiplayer Architecture

This document specifies the server-authoritative multiplayer architecture for Lithforge. It covers the full implementation sequence from command layer through DI refactoring, with design decisions, protocol details, and references to existing voxel engine implementations.

---

## Status Overview

| Priority | Name | Status | Depends On |
|----------|------|--------|------------|
| P6 | DI / Static Elimination | **Completed** | — |
| P1 | Command Layer | Not Started | P6 |
| P2 | Service Extraction | Not Started | P6 |
| P3 | Per-Player Physics | Not Started | P1, P2 |
| P4 | Chunk Protocol | Not Started | P2 |
| P5 | Inventory Synchronization | Not Started | P1, P2 |

---

## Implementation Sequence

```
P6  DI / Static Elimination ✅
 │
 ├──► P1  Command Layer
 │     │
 │     ├──► P3  Per-Player Physics  (needs P1 + P2)
 │     │
 │     └──► P5  Inventory Sync      (needs P1 + P2)
 │
 └──► P2  Service Extraction
       │
       ├──► P3  Per-Player Physics  (needs P1 + P2)
       │
       ├──► P4  Chunk Protocol      (needs P2)
       │
       └──► P5  Inventory Sync      (needs P1 + P2)
```

P1 and P2 can proceed in parallel after P6. P3, P4, P5 require their dependencies to be completed first.

---

## Priority 1: Command Layer

### Overview

All player actions are expressed as **blittable command structs** that flow through a deterministic pipeline: client creates command → serialize → network → server validates → server applies → server broadcasts result.

### Command Header

Every command includes a common header for ordering, deduplication, and attribution:

```
CommandHeader (blittable struct, 12 bytes)
├── Tick        : uint    — server tick this command targets
├── SequenceId  : ushort  — per-player monotonic sequence number
└── PlayerId    : ushort  — server-assigned player identifier
```

Sequence IDs enable the server to detect duplicates and the client to match acknowledgements to predictions. This follows the Minecraft 1.19+ acknowledgement system where the server echoes the last processed sequence ID per player.

### Command Types

| Command | Payload | Size (bytes) |
|---------|---------|-------------|
| `PlaceBlockCommand` | `int3 Position, StateId BlockState, byte Face` | 16 |
| `BreakBlockCommand` | `int3 Position` | 12 |
| `MoveCommand` | `float3 Position, float2 LookDir, byte InputFlags` | 24 |
| `InteractCommand` | `int3 Position, byte InteractionType` | 16 |
| `SlotClickCommand` | `short SlotIndex, byte ClickType, byte Button` | 4 |

All command structs are blittable (Tier 2 compatible). No managed references, no strings.

### Ring Buffer

Commands are stored in a **ring buffer indexed by server tick** on both client and server:

```
Client:
  CommandRingBuffer<T> (capacity = 256 ticks)
  ├── Store(tick, command)     — write predicted command
  ├── GetRange(fromTick, toTick) — replay for reconciliation
  └── Advance(ackedTick)       — discard acknowledged commands

Server:
  Per-player CommandQueue
  ├── Enqueue(command)         — received from network
  ├── DrainForTick(tick)       — process all commands for current tick
  └── Drop older than (tick - maxLatencyTicks)
```

### Server Validation Rules

The server validates every command before applying it. Invalid commands are silently dropped (no error response — the client self-corrects via state reconciliation).

| Rule | Applies To | Check |
|------|-----------|-------|
| Distance | Place, Break, Interact | `distance(player.pos, target) <= maxReach (5.0)` |
| Existence | Break, Interact | Target block is not air |
| Inventory | Place | Player inventory contains the placed block |
| Collision | Place | Target position is unoccupied (no entity overlap) |
| Rate limit | All | `sequenceId > lastProcessed[playerId]` |
| Chunk loaded | Place, Break | Target chunk is in `Ready` state on server |
| Tool requirement | Break | Player holds correct tool type (tag-based check) |

### References

- **Minecraft 1.19+**: Sequence number acknowledgement system, `ServerboundPlayerActionPacket`
- **Luanti**: `TOSERVER_INTERACT`, `TOSERVER_PLAYERPOS` (Sources/luanti-master/src/network/networkprotocol.h)

---

## Priority 2: Service Extraction

### Overview

Extract game logic from MonoBehaviours into plain C# services that can run in both Unity client and headless server contexts. This is the prerequisite for dedicated server builds.

### Three-Layer Split

```
Layer 1 — Pure C# Services (Tier 1/2)
  ├── IBlockActionProcessor    — validates + applies block changes
  ├── IWorldSimulation         — tick-driven world update loop
  ├── IInventoryService        — inventory manipulation + validation
  ├── IPlayerManager           — player session lifecycle
  └── ICraftingService         — recipe matching + consumption

Layer 2 — WorldSimulation Driver (Tier 2/3 boundary)
  ├── Owns ChunkManager, GenerationScheduler, MeshScheduler
  ├── Calls IBlockActionProcessor for validated edits
  ├── Runs generation pipeline, schedules Burst jobs
  └── Does NOT depend on UnityEngine for logic (only for job scheduling)

Layer 3 — Thin MonoBehaviour Shell (Tier 3)
  ├── GameLoop.Update() calls WorldSimulation.Tick()
  ├── Forwards rendered mesh data to MegaMeshBuffer
  ├── Handles input → command creation
  └── Manages Unity lifecycle (Awake, OnDestroy, scene transitions)
```

### IBlockActionProcessor Pattern

```
public interface IBlockActionProcessor
{
    BlockActionResult TryPlace(int3 position, StateId state, PlayerId actor);
    BlockActionResult TryBreak(int3 position, PlayerId actor);
}
```

The server implementation validates against the command rules (P1). The single-player implementation skips network validation but still uses the same interface — ensuring identical behavior.

### Server Build Strategy

```
#if UNITY_SERVER
  — Strip: Rendering, UI, Input, Audio, Shaders
  — Keep: ChunkManager, WorldSimulation, GenerationScheduler (jobs still run)
  — Replace: FrameProfiler → NullFrameProfiler, PipelineStats → NullPipelineStats
  — Add: NetworkListener, PlayerSessionManager
#endif
```

Assembly Definitions for server builds:
- `Lithforge.Runtime.Client.asmdef` — rendering, UI, input (excluded from server)
- `Lithforge.Runtime.Server.asmdef` — network listener, session management (excluded from client)
- `Lithforge.Runtime.Shared.asmdef` — GameLoop, schedulers, bootstrap (included in both)

### References

- **Rust dedicated servers**: Thin MonoBehaviour shell pattern, headless Unity builds
- **Luanti**: `Server` class (Sources/luanti-master/src/server.cpp) — pure C++ game loop, no rendering

---

## Priority 3: Per-Player Physics

### Overview

Server-authoritative player physics with client-side prediction and server reconciliation. The server simulates all players; clients predict locally and correct when the server disagrees.

### PlayerPhysicsState

```
PlayerPhysicsState (blittable struct, 48 bytes)
├── Position          : float3   — world-space feet position
├── Velocity          : float3   — current velocity (m/s)
├── IsGrounded        : bool     — touching ground
├── IsSprinting       : bool     — sprint modifier active
├── IsSwimming        : bool     — in water
├── LastProcessedInput: ushort   — last MoveCommand sequence ID applied
└── Tick              : uint     — server tick of this state
```

### Client Prediction Loop

```
1. Record input → create MoveCommand(sequenceId, inputFlags, lookDir)
2. Store command in local ring buffer
3. Apply command locally (predict): VoxelCollider.MoveAndSlide()
4. Send command to server
5. Receive authoritative state from server (includes lastProcessedInput)
6. Compare server state with predicted state at that sequence ID
7. If delta > threshold (0.01 units):
   a. Snap to server position
   b. Replay all unacknowledged commands (sequenceId > lastProcessedInput)
   c. Re-run VoxelCollider.MoveAndSlide() for each replayed command
```

### Server Simulation

The server processes `MoveCommand`s in tick order. For each player per tick:

```
1. Dequeue MoveCommand for this tick (or use last known input if none)
2. Apply physics: gravity, VoxelCollider.MoveAndSlide()
3. Validate position (anti-cheat: speed cap, noclip detection)
4. Update PlayerPhysicsState
5. Broadcast authoritative state to all clients
```

The existing `PlayerPhysicsBody` (tick-rate physics, `VoxelCollider` integration) maps directly to this per-player simulation. The fixed tick rate (30 TPS) ensures deterministic results.

### Burst-Parallel Simulation

Multiple players can be simulated in parallel using `IJobParallelFor`:

```
[BurstCompile]
public struct PlayerPhysicsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<MoveCommand> Commands;
    public NativeArray<PlayerPhysicsState> States;
    [ReadOnly] public NativeArray<StateId> ChunkData;  // world collision data

    public void Execute(int playerIndex) { ... }
}
```

This leverages the existing Burst/Jobs infrastructure. Player physics is embarrassingly parallel — each player's collision is independent.

### Entity Interpolation

Other players (not the local player) are rendered using **entity interpolation**:

```
Rendered position = lerp(lastState.Position, currentState.Position, alpha)
```

Where `alpha` is the fraction of time elapsed since the last server tick. This uses the same interpolation pattern already implemented in `PlayerPhysicsBody` (LateUpdate lerp between previous and current tick positions).

### References

- **Gabriel Gambetta**: "Fast-Paced Multiplayer" — client prediction, server reconciliation, entity interpolation
- **Overwatch GDC 2017**: "Overwatch Gameplay Architecture and Netcode" — deterministic simulation, input buffering
- **Luanti**: `PlayerSAO::step()` (Sources/luanti-master/src/server/player_sao.cpp) — server-side player physics

---

## Priority 4: Chunk Protocol

### Overview

Efficient chunk data transfer between server and client, optimized for the bandwidth constraints of voxel games. Full chunks are sent on initial load; subsequent changes use delta packets.

### Palette-Compressed Sections

Chunk data is compressed using a **tiered palette scheme** inspired by Minecraft's chunk format:

| Section Type | When Used | Bits per Block | Size (32³) |
|-------------|-----------|---------------|------------|
| Single-valued | All blocks identical | 0 | 4 bytes (just the StateId) |
| Indirect 4-bit | ≤ 16 unique states | 4 | 16 KB + palette |
| Indirect 8-bit | ≤ 256 unique states | 8 | 32 KB + palette |
| Direct 15-bit | > 256 unique states | 15 | 60 KB |

Most chunks use 4-bit or 8-bit encoding. Underground stone-only chunks use single-valued. The palette is a sorted array of `StateId` values, transmitted alongside the section data.

### Packet Types

| Packet | Direction | Content | Delivery |
|--------|-----------|---------|----------|
| `ChunkDataPacket` | S→C | Full palette-compressed chunk + light data | Reliable ordered |
| `BlockChangePacket` | S→C | `int3 Position, StateId NewState` | Reliable ordered |
| `MultiBlockChangePacket` | S→C | `int ChunkX/Y/Z, (localIndex, StateId)[]` | Reliable ordered |
| `ChunkUnloadPacket` | S→C | `int3 ChunkCoord` | Reliable ordered |
| `ChunkRequestPacket` | C→S | `int3 ChunkCoord` | Reliable ordered |

### Compression

- **Algorithm**: zstd (level 1-3) for chunk data, not zlib/deflate
- **Rationale**: zstd at level 1 matches zlib-6 compression ratio at 3-5x the speed. Level 3 provides better ratio for initial chunk loads where latency tolerance is higher.
- **Existing integration**: `ChunkSerializer` already uses DeflateStream; migrating to zstd for network packets is a separate concern from storage compression.

### Delta Updates

Block changes within a loaded chunk use delta packets instead of retransmitting the full chunk:

```
Single block change:  BlockChangePacket (16 bytes)
Multi-block change:   MultiBlockChangePacket (variable, batched per chunk per tick)
```

The server batches all block changes within a single tick into one `MultiBlockChangePacket` per affected chunk, reducing packet count.

### Bandwidth Estimates (View Distance 8)

| Metric | Value |
|--------|-------|
| Chunks in view | ~4,900 (sphere) |
| Average compressed chunk | ~8 KB (palette + zstd) |
| Initial world load | ~39 MB (streamed over ~30s) |
| Steady-state (walking) | ~50-200 KB/s (new chunks at periphery) |
| Block changes | ~100-500 B/s (typical gameplay) |
| Player state updates | ~1.5 KB/s per player (30 TPS × 48B state) |

### Delivery Guarantees

| Data Type | Transport | Rationale |
|-----------|-----------|-----------|
| Block changes | Reliable ordered | Missed block change = permanent desync |
| Chunk data | Reliable ordered | Partial chunk = rendering artifacts |
| Player position | Unreliable sequenced | Stale position is worse than missed position |
| Commands (C→S) | Reliable ordered | Missed command = lost player action |

### References

- **Minecraft**: Chunk section palette encoding, `ClientboundLevelChunkPacket`
- **Luanti**: `NetworkPacket` (Sources/luanti-master/src/network/networkpacket.h), TOCLIENT_BLOCKDATA
- **Minosoft**: `ChunkUtil.readPaletteContainer()` (Sources/Minosoft-master/)

---

## Priority 5: Inventory Synchronization

### Overview

Server-authoritative inventory with client-side display. The server owns all inventory state; the client renders what the server tells it and sends click commands.

### State ID System

Each container (player inventory, chest, furnace) has a **state ID** — an integer that increments on every server-side mutation. This follows the Minecraft 1.17+ pattern:

```
1. Server mutates inventory → increment stateId
2. Server sends InventoryStatePacket(stateId, fullSlotData)
3. Client receives → updates local display, stores stateId
4. Client clicks slot → sends SlotClickCommand(stateId, slotIndex, clickType)
5. Server receives → if stateId matches current: apply; else: resync
```

If the client's state ID is stale (another source mutated the inventory), the server rejects the click and sends a full inventory resync.

### Server Validation

| Check | Description |
|-------|-------------|
| State ID match | Client stateId == server stateId for the container |
| Item conservation | Total item count before == total item count after |
| Range check | Slot index within container bounds |
| Slot validity | Target slot accepts the item type (e.g., fuel slot, output slot) |
| Stack limit | Resulting stack does not exceed max stack size |
| Container open | Player has the container open (distance check for block entities) |

### Client Prediction Strategy

For v1, **skip client-side inventory prediction**. Rationale:

- Inventory interactions are infrequent (1-5 per second, not 30-60 per second like movement)
- 50-100ms round-trip latency is barely perceptible for click-based interactions
- Prediction complexity is high (item splitting, shift-click routing, crafting output calculation)
- Incorrect prediction requires visual rollback, which feels worse than slight latency

If latency becomes a concern, client prediction can be added later without protocol changes — the state ID system supports it.

### QUICK_CRAFT (Paint Drag) Buffering

The `SlotInteractionController` paint-drag mode (left-click drag across slots to distribute items) requires special handling:

```
Phase 1: Client sends QUICK_CRAFT_BEGIN(stateId)
Phase 2: Client sends QUICK_CRAFT_SLOT(slotIndex) for each dragged-over slot
Phase 3: Client sends QUICK_CRAFT_END

Server buffers slot indices during phases 1-2, then applies the distribution atomically in phase 3.
```

This three-phase buffering prevents partial application if the connection drops mid-drag.

### Packet Types

| Packet | Direction | Content |
|--------|-----------|---------|
| `InventoryStatePacket` | S→C | `byte ContainerId, int StateId, ItemStack[] Slots` |
| `SlotClickCommand` | C→S | `byte ContainerId, int StateId, short SlotIndex, byte ClickType` |
| `QuickCraftPacket` | C→S | `byte Phase, short SlotIndex` |
| `HeldItemChangePacket` | C→S | `byte SlotIndex` (hotbar selection) |

### References

- **Minecraft 1.17+**: State ID acknowledgement system, `ServerboundContainerClickPacket`
- **Minosoft**: Container synchronization (Sources/Minosoft-master/)

---

## Priority 6: DI / Static Elimination — Completed

### Summary

Converted four static singletons to instance-based types with interfaces and Null Object pattern. Manual constructor/setter injection in `LithforgeBootstrap` — no DI container. See **ADR-004** for the full decision record.

### New Types Created

| Type | File | Purpose |
|------|------|---------|
| `IFrameProfiler` | `Assets/Lithforge.Runtime/Debug/IFrameProfiler.cs` | Interface for frame profiling |
| `NullFrameProfiler` | `Assets/Lithforge.Runtime/Debug/NullFrameProfiler.cs` | No-op implementation for headless/test |
| `IPipelineStats` | `Assets/Lithforge.Runtime/Debug/IPipelineStats.cs` | Interface for pipeline counters |
| `NullPipelineStats` | `Assets/Lithforge.Runtime/Debug/NullPipelineStats.cs` | No-op implementation for headless/test |
| `FrameProfilerSections` | `Assets/Lithforge.Runtime/Debug/FrameProfilerSections.cs` | Extracted section index constants |
| `WorldSession` | `Assets/Lithforge.Runtime/World/WorldSession.cs` | Immutable session parameters (replaces static WorldLauncher) |

### Statics Converted

| Before | After |
|--------|-------|
| `FrameProfiler` (static methods + arrays) | `FrameProfiler : IFrameProfiler` (sealed class, instance) |
| `PipelineStats` (static counters) | `PipelineStats : IPipelineStats` (sealed class, instance) |
| `WorldLauncher` (static mailbox) | `WorldSession` (immutable value object, passed via method argument) |

### Files Modified (~17)

`LithforgeBootstrap.cs`, `GameLoop.cs`, `MetricsRegistry.cs`, `F3DebugOverlay.cs`, `BenchmarkRunner.cs`, `GenerationScheduler.cs`, `MeshScheduler.cs`, `LODScheduler.cs`, `ChunkMeshStore.cs`, `MegaMeshBuffer.cs`, and their consumers.

### What This Unblocks

- **P1 (Command Layer)**: No direct dependency, but clean instance-based wiring sets the pattern for command processors.
- **P2 (Service Extraction)**: Services accept `IFrameProfiler`/`IPipelineStats` via constructor — no static coupling. `WorldSession` enables per-session service graphs.
- **P3 (Per-Player Physics)**: Per-world `WorldSession` enables isolated physics state per game session.
- **Headless server**: Null objects eliminate `#if UNITY_SERVER` conditionals around profiling calls.

---

## Key Design Decisions

| Decision | Choice | Alternative Rejected | Rationale |
|----------|--------|---------------------|-----------|
| Authority model | Server-authoritative | Peer-to-peer, lockstep | Voxel worlds are too large for lockstep; P2P cannot prevent cheating |
| DI approach | Manual constructor injection | VContainer, Zenject | Only ~4 bindings needed; container adds dependency + complexity (ADR-004) |
| Null Object pattern | NullFrameProfiler, NullPipelineStats | `#if UNITY_SERVER` conditionals | Zero null checks on hot paths; cleaner than preprocessor directives |
| Command transport | Blittable structs + ring buffer | Protobuf, FlatBuffers | Blittable structs are Burst-compatible; no serialization library dependency |
| Chunk compression | zstd (level 1-3) | zlib, LZ4, uncompressed | zstd-1 matches zlib-6 ratio at 3-5x speed; LZ4 ratio too low for chunks |
| Physics prediction | Client-side prediction + reconciliation | Server-only (input delay) | Input delay unacceptable at >50ms RTT for movement |
| Inventory prediction | Server-only for v1 | Full client prediction | Click interactions tolerate 50-100ms latency; prediction complexity not justified |
| Chunk delta format | Per-block + multi-block batch packets | Full chunk retransmit | Single block changes are 16 bytes vs ~8 KB for full chunk |
| Player state delivery | Unreliable sequenced | Reliable ordered | Stale position data is worse than dropped data; reliable adds head-of-line blocking |
| Session data passing | Immutable `WorldSession` object | Static mailbox (WorldLauncher) | Type-safe, no temporal coupling, supports multi-world |

---

## Reference Sources

| Source | Location | Covers |
|--------|----------|--------|
| Luanti (Minetest) | `Sources/luanti-master/` | Network protocol, server architecture, block interaction packets |
| Minosoft | `Sources/Minosoft-master/` | Chunk palette decoding, container sync, protocol implementation |
| Gabriel Gambetta | Web article series | Client prediction, server reconciliation, entity interpolation theory |
| Overwatch GDC 2017 | GDC Vault | Deterministic simulation, input buffering, netcode architecture |
| Minecraft Wiki | Protocol documentation | Chunk format, state IDs, sequence numbers, packet specifications |
| Lithforge ADR-004 | `Docs/adr/ADR-004_instance_based_di_for_multiplayer.md` | DI refactor decision record |
