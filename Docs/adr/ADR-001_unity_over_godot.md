# ADR-001: Unity Over Godot

## Status
Accepted

## Context
Lithforge needs a game engine that supports high-performance voxel rendering with Burst-compiled jobs, NativeContainers for cache-friendly data layouts, and a mature rendering pipeline (URP) for cross-platform deployment. The two primary candidates were Unity and Godot.

## Decision
Unity was chosen as the engine for Lithforge.

## Rationale

### Unity Advantages
- **Burst Compiler**: Generates SIMD-optimized native code from C# job structs. Essential for greedy meshing, terrain generation, and light propagation at target performance (< 0.5ms per chunk mesh).
- **NativeContainers**: `NativeArray<T>`, `NativeList<T>`, `NativeQueue<T>` provide cache-friendly, blittable data storage with safety checks in editor and zero overhead in builds.
- **Job System**: Built-in work-stealing thread pool with dependency tracking. No manual thread management needed.
- **URP (Universal Render Pipeline)**: Customizable, cross-platform, supports custom shaders with HLSL. Mature tooling for profiling and debugging.
- **Mesh.MeshDataArray API**: Allows writing mesh data from jobs and uploading to GPU efficiently.
- **Ecosystem maturity**: Profiler, Frame Debugger, Memory Profiler, Package Manager, Test Framework.
- **Platform targets**: Windows, macOS, Linux, Android, iOS, WebGL from one codebase.

### Godot Limitations (for this project)
- No equivalent to Burst Compiler for SIMD-optimized C# jobs.
- GDNative/GDExtension requires C++ for performance-critical paths, complicating the development model.
- Mesh generation APIs less mature for high-throughput voxel rendering.
- Smaller ecosystem for profiling and native memory debugging.

## Consequences
- The project is coupled to Unity's release cycle and licensing terms.
- The three-tier architecture (ADR-002) mitigates coupling by isolating pure C# logic in Tier 1.
- Contributors need Unity experience or willingness to learn the Unity workflow.
