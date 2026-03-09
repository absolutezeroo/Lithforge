# ADR-003: 32x32x32 Chunk Size

## Status
Accepted

## Context
Chunk dimensions affect greedy meshing performance, memory usage, rebuild cost on block changes, and cache efficiency. Common choices in voxel engines are 16x16x16 (Minecraft sections), 32x32x32, and 64x64x64.

## Decision
Lithforge uses **32x32x32** chunks (32,768 voxels per chunk).

## Rationale

### Why 32 (Not 16)
- **Greedy meshing alignment**: 32-bit integers map perfectly to one row of a 32-wide chunk face. The greedy meshing algorithm uses bitmask rows to merge adjacent faces — a 32-wide slice fits exactly in a `uint`, enabling efficient bitwise operations without multi-word arithmetic.
- **Fewer chunks to manage**: At 16-chunk render distance, 32-cubed chunks require ~10,000 loaded chunks vs ~80,000 with 16-cubed. This reduces ChunkManager dictionary pressure, job scheduling overhead, and GPU draw calls.
- **Better GPU batching**: Larger chunks produce fewer draw calls (one mesh per chunk per render layer). With 16-cubed chunks, draw call count would be 8x higher.

### Why 32 (Not 64)
- **Rebuild cost**: When a single block changes, the entire chunk must be remeshed. A 64-cubed chunk has 262,144 voxels — remeshing takes ~4x longer than 32-cubed, causing visible stutters on block placement.
- **Memory granularity**: Each 64-cubed chunk uses 512 KB of NativeArray storage (vs 64 KB for 32-cubed). Loading/unloading granularity is too coarse.
- **Neighbor data overhead**: Cross-chunk meshing needs border slices from 6 neighbors. With 64-cubed chunks, each border slice is 4,096 StateIds (vs 1,024 for 32-cubed).

### Memory per Chunk
- Dense `NativeArray<StateId>`: 32,768 x 2 bytes = 64 KB
- Light data: 32,768 x 1 byte = 32 KB
- Typical mesh: 10-50 KB
- Total per loaded chunk: ~80-160 KB

### Index Layout
Y-major layout: `index = y * 1024 + z * 32 + x`. This groups horizontal slices contiguously, which is cache-friendly for both greedy meshing (iterates one Y-slice at a time) and sunlight propagation (iterates top-down by Y).

## Consequences
- `ChunkConstants.Size = 32` is hardcoded and used throughout the codebase.
- All Burst jobs assume 32-wide slices. Changing chunk size would require updating meshing, lighting, and worldgen jobs.
- The `ChunkPool` pre-allocates arrays of exactly 32,768 elements.
