# Lithforge — Observability & Diagnostics

## Metrics Catalogue

### Per-Frame Metrics

| Metric | Unit | Source | ProfilerMarker |
|--------|------|--------|---------------|
| Frame time | ms | Unity | `Lithforge.Frame` |
| Main thread budget used | ms | GameLoop | `Lithforge.MainThread` |
| Mesh uploads this frame | count | MeshUploader | — |
| Generation results processed | count | ChunkManager | — |

### Chunk Pipeline Metrics

| Metric | Unit | Source | ProfilerMarker |
|--------|------|--------|---------------|
| Chunk generation time | ms | GenerationPipeline | `Lithforge.ChunkGen` |
| TerrainShapeJob time | ms | Job profiling | `Lithforge.TerrainShape` |
| CaveCarverJob time | ms | Job profiling | `Lithforge.CaveCarver` |
| GreedyMeshJob time | ms | Job profiling | `Lithforge.GreedyMesh` |
| Mesh vertex count (per chunk) | count | MeshData | — |
| Mesh triangle count (per chunk) | count | MeshData | — |
| Light propagation time | ms | LightEngine | `Lithforge.LightProp` |

### World State Metrics

| Metric | Unit | Source |
|--------|------|--------|
| Loaded chunks | count | ChunkManager |
| Chunks in state Generating | count | ChunkManager |
| Chunks in state Meshing | count | ChunkManager |
| Chunks in state Dirty | count | ChunkManager |
| Pending generation jobs | count | Job queue |
| Pending mesh jobs | count | Job queue |
| ChunkPool available | count | ChunkPool |
| ChunkPool total allocated | count | ChunkPool |

### Memory Metrics

| Metric | Unit | Source |
|--------|------|--------|
| Native memory (NativeArrays) | MB | Unity.Collections tracking |
| Managed heap | MB | GC.GetTotalMemory |
| GPU memory (meshes) | MB | Profiler.GetAllocatedMemoryForGraphicsDriver |
| Texture atlas memory | MB | Texture2DArray size |

---

## Unity Profiler Integration

All hot paths use `Unity.Profiling.ProfilerMarker` for timeline visibility:

```csharp
public sealed class ChunkManager
{
    private static readonly ProfilerMarker s_updateMarker =
        new ProfilerMarker("Lithforge.ChunkManager.Update");
    private static readonly ProfilerMarker s_meshUploadMarker =
        new ProfilerMarker("Lithforge.MeshUpload");

    public void Update(float3 playerPosition, int renderDistance)
    {
        using (s_updateMarker.Auto())
        {
            // ...
        }
    }
}
```

For Burst jobs, use `Unity.Profiling.ProfilerMarker` within the job's `Execute()`:

```csharp
[BurstCompile]
public struct GreedyMeshJob : IJob
{
    private static readonly ProfilerMarker s_marker =
        new ProfilerMarker("Lithforge.GreedyMeshJob");

    public void Execute()
    {
        s_marker.Begin();
        // ... greedy meshing logic ...
        s_marker.End();
    }
}
```

---

## Debug Overlay (In-Game)

An IMGUI-based overlay toggled with F3 (matching Minecraft convention):

```
┌─────────────────────────────────────────┐
│ Lithforge v0.1.0 (URP)                  │
│ FPS: 62 (16.1ms)  GPU: 4.2ms            │
│                                          │
│ Chunks: 4821 loaded / 312 meshed/frame   │
│ Gen queue: 24   Mesh queue: 8            │
│ Pool: 128/512 available                  │
│                                          │
│ Player: (128, 72, -256)                  │
│ Chunk: (4, 2, -8)  Biome: plains         │
│ Target: stone [lit=false]                │
│                                          │
│ Memory: 842MB native / 124MB managed     │
│ Draw calls: 1247  Triangles: 2.1M        │
│ LOD: 2841/0/1248/732 (L0/L1/L2/L3)      │
└─────────────────────────────────────────┘
```

---

## Logging

### Log Categories

| Category | Examples |
|----------|---------|
| `Lithforge.Content` | Content loading, validation errors/warnings |
| `Lithforge.Chunk` | Chunk lifecycle transitions, save/load |
| `Lithforge.WorldGen` | Generation pipeline timing, errors |
| `Lithforge.Meshing` | Mesh build times, vertex counts |
| `Lithforge.Rendering` | Shader compilation, material creation |
| `Lithforge.Modding` | Mod discovery, load failures |
| `Lithforge.Entity` | Entity creation, destruction |

### Log Levels

| Level | Usage |
|-------|-------|
| `Error` | Something failed that should not have. Fallback applied or operation skipped. |
| `Warning` | Unexpected but recoverable. Missing texture, unknown StateId. |
| `Info` | Significant lifecycle events. Content loaded, world saved, mod activated. |
| `Debug` | Detailed operational info. Chunk state transitions, job scheduling. |
| `Trace` | Extremely verbose. Per-voxel operations. Disabled in builds. |

### Implementation

```csharp
// Tier 1 interface
public interface ILogger
{
    void Log(LogLevel level, string category, string message);
}

// Tier 3 Unity bridge
public sealed class UnityLogger : ILogger
{
    public void Log(LogLevel level, string category, string message)
    {
        string formatted = $"[{category}] {message}";
        switch (level)
        {
            case LogLevel.Error:   Debug.LogError(formatted);   break;
            case LogLevel.Warning: Debug.LogWarning(formatted); break;
            default:               Debug.Log(formatted);        break;
        }
    }
}
```

---

## Benchmark Suite

### Pure C# Benchmarks (BenchmarkDotNet)

Run outside Unity for precise measurement of Tier 1/2 algorithms:

```
Lithforge.Benchmarks/
├── GreedyMeshBenchmark.cs       # 32³ chunk meshing, varying block distributions
├── NoiseSamplingBenchmark.cs    # 2D/3D noise at various octave counts
├── StateRegistryBenchmark.cs   # Lookup throughput
├── PaletteCompressionBenchmark.cs
└── ChunkSerializerBenchmark.cs  # Serialize + zstd compress
```

### Unity Performance Tests

Using Unity's Performance Testing package for in-engine measurement:

```
Assets/Tests/Performance/
├── ChunkGenerationPerfTest.cs   # Full pipeline, measured with ProfilerMarker
├── MeshUploadPerfTest.cs        # Mesh.ApplyAndDisposeWritableMeshData timing
├── RenderPerfTest.cs            # FPS at various render distances
└── ContentLoadPerfTest.cs       # Full content loading pipeline timing
```

### Performance Acceptance Criteria

| Metric | Target | CI Enforcement |
|--------|--------|---------------|
| GreedyMeshJob (32³ surface chunk) | < 0.5ms | BenchmarkDotNet, fail if > 0.7ms |
| TerrainShapeJob (32³) | < 0.3ms | BenchmarkDotNet, fail if > 0.5ms |
| NativeNoise.Sample2D (1M samples) | < 5ms | BenchmarkDotNet |
| Content load (core only) | < 1s | Unity perf test |
| 16-chunk render distance | > 60 FPS | Manual benchmark (GPU-dependent) |
