# Lithforge — Reference Analysis: Luanti & Minosoft

## Luanti Source Code Analysis

Based on examination of the Luanti source code at `github.com/luanti-org/luanti`.

### Source Structure

```
src/
├── mapblock.cpp/h              # MapBlock: 16³ node container
├── map.cpp/h                   # Map: manages MapBlocks
├── mapnode.cpp/h               # MapNode: content_t(u16) + param1(u8) + param2(u8)
├── nodedef.cpp/h               # ContentFeatures: monolithic node definition
├── voxel.cpp/h                 # VoxelManipulator: bulk voxel operations
├── voxelalgorithms.cpp/h       # Light propagation BFS
├── emerge.cpp/h                # EmergeManager: async generation coordinator
├── mapgen/
│   ├── mapgen.cpp/h            # Base Mapgen class
│   ├── mapgen_v7.cpp/h         # Mountains, ridges, floatlands, caverns
│   ├── mapgen_valleys.cpp/h    # River valleys
│   ├── mapgen_carpathian.cpp/h # Mountain ranges
│   ├── mg_biome.cpp/h          # Temperature/humidity biome selection
│   ├── mg_ore.cpp/h            # 6 ore types: scatter, sheet, puff, blob, vein, stratum
│   ├── mg_decoration.cpp/h     # Surface decorations
│   ├── mg_schematic.cpp/h      # MTSM voxel templates
│   ├── cavegen.cpp/h           # Cave generation
│   └── treegen.cpp/h           # L-system trees
├── client/
│   ├── mapblock_mesh.cpp/h     # Mesh from MapBlock (no greedy meshing)
│   ├── mesh_generator_thread.h # MeshUpdateQueue
│   └── content_mapblock.cpp/h  # DrawType-based geometry
└── gui/
    └── guiFormSpecMenu.cpp/h   # Formspec UI (to be replaced)
```

### Key Issues Lithforge Addresses

| Luanti Limitation | Lithforge Solution |
|-------------------|-------------------|
| Monolithic C++ `ContentFeatures` (50+ fields mixing render, physics, gameplay) | Separated across BlockDefinition (logic), BlockStateCompact (cached render flags), BlockModelDefinition (geometry) |
| No greedy meshing (per-face emission) | Binary greedy meshing in Burst-compiled job |
| No LOD | 4-level LOD system |
| 16³ MapBlocks (poor greedy ratio, many boundaries) | 32³ chunks aligned with uint32 bitwise ops |
| `param2` limited to 8 bits for all metadata | Full property system with pre-computed state registry |
| Formspec UI | Unity UI Toolkit + uGUI |
| Single-threaded meshing/lighting | Unity Job System + Burst for all hot paths |
| Lua-only scripting (server-side) | Data-driven mods (V1) + C# mods (V2) |

### What We Take From Luanti

- 6 ore types (scatter, sheet, puff, blob, vein, stratum) — reimplemented as Burst jobs
- Temperature/humidity biome selection — reimplemented as Burst job
- Schematic format concept — custom binary format with zstd
- EmergeManager pattern — generalized as job-based ChunkManager
- Multiple mapgen types — configurable pipeline stages instead of class hierarchy
- Content DB concept — built-in content browser

---

## Minosoft Architecture Analysis

Kotlin Minecraft reimplementation at `github.com/Bixilon/Minosoft`.

### Architecture Patterns Adopted

| Minosoft Pattern | Lithforge Adoption |
|-----------------|-------------------|
| Rendering independent from GUI | Tier 3 Rendering ≠ Tier 3 UI |
| Event/watcher driven | EventBus in Core (Tier 1, managed, main-thread) |
| Aggressive multi-threading | Unity Job System + Burst (structured, not ad-hoc) |
| Data-driven block/item properties | Full Minecraft-convention data files |
| Hash-based asset deduplication | Content-addressed storage for mod assets |
| Simple code for performance | Burst compilation makes simple loops fast |

### Key Lesson: Performance From Simplicity

Minosoft's performance doc emphasizes simple code over clever code. Lithforge applies this via Burst: write straightforward loops over NativeArrays, let the Burst compiler optimize (vectorize, inline, remove bounds checks). No manual SIMD intrinsics, no pointer arithmetic, no unsafe code — Burst handles it.

---

## Comparative Feature Matrix

| Feature | Luanti | Minosoft | Minecraft | Lithforge |
|---------|--------|----------|-----------|-----------|
| Language | C++ | Kotlin/Java | Java | C# |
| Engine | Custom (Irrlicht) | Custom (OpenGL) | Custom (LWJGL) | **Unity** |
| Compilation | Native | JVM | JVM | **Burst (native via IL)** |
| Chunk size | 16³ | 16×384×16 | 16×384×16 | **32³** |
| Block state | u16 + u8×2 | MC states | Properties + palette | **StateId(u16) + NativeStateRegistry** |
| Meshing | Per-face | Greedy-ish | Greedy-ish | **Binary greedy (Burst)** |
| LOD | None | None | None (vanilla) | **4-level LOD** |
| Threading | EmergeThread | Kotlin coroutines | Dedicated threads | **Unity Jobs + Burst** |
| ECS | None | None | None | **DOTS (entities only)** |
| Modding | Lua scripts | Limited | Java (Fabric/Forge) | **Data mods (V1), C# (V2)** |
| UI | Formspecs | JavaFX | Custom | **UI Toolkit + uGUI** |
| Render pipeline | Irrlicht | Raw OpenGL | Custom | **URP** |
| Data format | C++ structs + Lua | PixLyzer JSON | Hardcoded + datapacks | **JSON content files** |
| World storage | Custom | MC Anvil | Anvil regions | **Custom regions + zstd** |
| Profiling | Basic timers | JVM profiler | JVM profiler | **Unity Profiler + ProfilerMarker** |
| Asset format | Custom | MC format | MC format | **Texture2DArray + JSON models** |
