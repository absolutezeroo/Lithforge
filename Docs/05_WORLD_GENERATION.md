# Lithforge — World Generation Pipeline

## Pipeline Architecture

World generation is an ordered pipeline of stages. The pipeline **orchestrator** is managed C# code that schedules Burst-compiled jobs sequentially via `JobHandle` dependencies. Each stage's heavy computation runs in a `[BurstCompile]` job on a worker thread.

```
GenerationPipeline (managed orchestrator)
│
├── TerrainShapeJob        [BurstCompile]  priority: 0
├── CaveCarverJob          [BurstCompile]  priority: 100
├── BiomeAssignmentJob     [BurstCompile]  priority: 200
├── SurfaceBuilderJob      [BurstCompile]  priority: 300
├── OreGenerationJob       [BurstCompile]  priority: 400
├── InitialLightingJob     [BurstCompile]  priority: 500
└── LightPropagationJob    [BurstCompile]  priority: 600
```

**Note**: `DecorationStage` (trees) runs on the main thread after generation jobs complete, not as part of the Burst job chain. It is invoked by `GenerationScheduler` after the `GenerationHandle` completes.

### Why DecorationStage Is Not a Burst Job

Tree placement may cross chunk boundaries, requiring writes to `PendingDecorationStore` (managed collection). The actual block writes are simple `StateId` assignments. Cross-boundary blocks are stored as `PendingBlock` entries and applied when the target chunk reaches its own `Decorate()` phase.

**StructureStage** (villages, dungeons) is listed in the pipeline design but NOT yet implemented.

### Pipeline Execution

```csharp
public sealed class GenerationPipeline
{
    private readonly NativeNoiseConfig _terrainNoise;
    private readonly NativeNoiseConfig _caveNoise;
    private readonly NativeArray<NativeBiomeData> _biomeData;
    private readonly NativeArray<NativeOreConfig> _oreConfigs;
    private readonly NativeStateRegistry _stateRegistry;
    private readonly StateId _stoneId, _airId, _waterId;

    /// <summary>
    /// Schedules the full generation chain for one chunk.
    /// Returns the final JobHandle. Caller must Complete() before reading chunk data.
    /// All NativeArrays in context are allocated with TempJob and must be disposed after completion.
    /// </summary>
    public GenerationHandle Schedule(int3 coord, long seed, NativeArray<StateId> chunkData)
    {
        NativeArray<int> heightMap = new NativeArray<int>(1024, Allocator.TempJob);
        NativeArray<byte> biomeMap = new NativeArray<byte>(1024, Allocator.TempJob);
        NativeArray<float> tempMap = new NativeArray<float>(1024, Allocator.TempJob);
        NativeArray<float> humidMap = new NativeArray<float>(1024, Allocator.TempJob);

        JobHandle h0 = new TerrainShapeJob
        {
            ChunkData = chunkData,
            HeightMap = heightMap,
            Seed = seed,
            ChunkCoord = coord,
            NoiseConfig = _terrainNoise,
            SeaLevel = 64,
            StoneId = _stoneId,
            WaterId = _waterId,
            AirId = _airId,
        }.Schedule();

        JobHandle h1 = new CaveCarverJob
        {
            ChunkData = chunkData,
            Seed = seed,
            ChunkCoord = coord,
            CaveConfig = _caveNoise,
            AirId = _airId,
        }.Schedule(h0);

        JobHandle h2 = new BiomeAssignmentJob
        {
            HeightMap = heightMap,
            BiomeMap = biomeMap,
            TemperatureMap = tempMap,
            HumidityMap = humidMap,
            BiomeData = _biomeData,
            Seed = seed,
            ChunkCoord = coord,
        }.Schedule(h1);

        JobHandle h3 = new SurfaceBuilderJob
        {
            ChunkData = chunkData,
            HeightMap = heightMap,
            BiomeMap = biomeMap,
            BiomeData = _biomeData,
            ChunkCoord = coord,
            SeaLevel = 64,
        }.Schedule(h2);

        JobHandle h4 = new OreGenerationJob
        {
            ChunkData = chunkData,
            OreConfigs = _oreConfigs,
            Seed = seed,
            ChunkCoord = coord,
            StateRegistry = _stateRegistry,
        }.Schedule(h3);

        JobHandle h5 = new InitialLightingJob
        {
            ChunkData = chunkData,
            HeightMap = heightMap,
            StateRegistry = _stateRegistry,
            // Output: writes to light data NativeArray
        }.Schedule(h4);

        return new GenerationHandle
        {
            FinalHandle = h5,
            HeightMap = heightMap,
            BiomeMap = biomeMap,
            TemperatureMap = tempMap,
            HumidityMap = humidMap,
        };
    }
}

/// <summary>
/// Tracks a generation in flight. Caller must Complete() then Dispose().
/// </summary>
public struct GenerationHandle : System.IDisposable
{
    public JobHandle FinalHandle;
    public NativeArray<int> HeightMap;
    public NativeArray<byte> BiomeMap;
    public NativeArray<float> TemperatureMap;
    public NativeArray<float> HumidityMap;

    public void Dispose()
    {
        if (HeightMap.IsCreated) HeightMap.Dispose();
        if (BiomeMap.IsCreated) BiomeMap.Dispose();
        if (TemperatureMap.IsCreated) TemperatureMap.Dispose();
        if (HumidityMap.IsCreated) HumidityMap.Dispose();
    }
}
```

---

## Stage Details

### TerrainShapeJob

Uses **2D heightmap** noise to determine terrain shape. Each column (x, z) gets a single surface height derived from `NativeNoise.Sample2D()`:

```
surfaceY = SeaLevel + round(noiseValue)
```

For each voxel: below `surfaceY` → stone, between `surfaceY` and `SeaLevel` → water, above both → air.

The `HeightMap` output (32×32 = 1024 entries) is reused by `BiomeAssignmentJob` and `SurfaceBuilderJob`.

**Note**: 3D density-based terrain (overhangs, floating islands) is NOT currently implemented. The terrain is strictly a heightmap with no overhangs. 3D terrain generation via density noise is a planned future enhancement.

### NativeNoise — Burst-Compatible Noise

`FastNoiseLite` is a managed C# library and cannot be used in Burst. We need a Burst-compatible noise implementation using `Unity.Mathematics`:

```csharp
/// <summary>
/// Burst-compatible noise sampling using Unity.Mathematics.noise.
/// Supports FBM (fractal Brownian motion) with configurable octaves.
/// </summary>
public static class NativeNoise
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sample2D(float x, float z, NativeNoiseConfig config, long seed)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = config.Frequency;
        float maxValue = 0f;

        for (int o = 0; o < config.Octaves; o++)
        {
            float sx = (x + seed + config.SeedOffset) * frequency;
            float sz = (z + seed + config.SeedOffset) * frequency;
            value += noise.snoise(new float2(sx, sz)) * amplitude;
            maxValue += amplitude;
            amplitude *= config.Persistence;
            frequency *= config.Lacunarity;
        }

        return value / maxValue; // normalized to [-1, 1]
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sample3D(float x, float y, float z, NativeNoiseConfig config, long seed)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = config.Frequency;
        float maxValue = 0f;

        for (int o = 0; o < config.Octaves; o++)
        {
            float sx = (x + seed + config.SeedOffset) * frequency;
            float sy = (y + seed + config.SeedOffset + 31337) * frequency;
            float sz = (z + seed + config.SeedOffset + 71093) * frequency;
            value += noise.snoise(new float3(sx, sy, sz)) * amplitude;
            maxValue += amplitude;
            amplitude *= config.Persistence;
            frequency *= config.Lacunarity;
        }

        return value / maxValue;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeNoiseConfig
{
    public float Frequency;
    public float Lacunarity;
    public float Persistence;
    public int Octaves;
    public float HeightScale;
    public int SeedOffset;
}
```

### Ore Types

Two ore types are currently implemented in `OreGenerationJob` (Burst):

| Type | Algorithm | Parameters |
|------|-----------|------------|
| Scatter | Random positions at configured frequency | minY, maxY, frequency |
| Blob | Spheroid clusters at random centers | veinSize, minY, maxY, frequency |

Each ore uses a deterministic `Random` seeded per ore per chunk. The job replaces `replaceBlock` (usually stone) with `oreBlock` when placement conditions are met.

Additional ore types from Luanti (Vein, Sheet, Stratum, Puff) are planned but not yet implemented.

### Tree Templates and Per-Biome Selection

Three tree templates are defined in `TreeTemplate.cs` as static methods:

| Index | Template | Shape |
|-------|----------|-------|
| 0 | `OakTree` | 5-block trunk, 5×5×3 canopy + 3×3 top |
| 1 | `BirchTree` | 7-block trunk (tall), 3×3×3 canopy + 1×1 top |
| 2 | `SpruceTree` | 6-block trunk, tapering 5×5→3×3→1×1 canopy |

All three currently use oak_log and oak_leaves StateIds (no separate wood types yet — shapes differ but materials are identical).

Each `BiomeDefinition` has a `treeType` field (0–2) that maps to `NativeBiomeData.TreeTemplateIndex`. `DecorationStage` reads this per-column to select the template.

Tree placement per column: read `biomeId` from biomeMap → fetch `NativeBiomeData` (O(1) direct index) → if `TreeDensity > 0`, roll deterministic hash from world XZ + seed → if `roll < TreeDensity`, call `PlaceTree(biome.TreeTemplateIndex)`.

### Cross-Chunk Decoration Handling

Trees that cross chunk boundaries use a **pending decorations** system via `PendingDecorationStore`:

```
1. During DecorationStage for chunk C:
   - For each tree block to place:
     - Blocks within C → write directly to ChunkData
     - Blocks outside C → compute target chunk coord, store as PendingBlock in PendingDecorationStore

2. When neighbor chunk N runs Decorate():
   - First calls _pendingStore.ApplyPending(coord) to apply deferred blocks from other chunks
   - Then runs its own tree placement

3. PendingDecorationStore is managed code (not Burst).
   The actual block writes are simple StateId assignments.
```

### CaveCarverJob — Depth Variation

Spaghetti caves use two 3D noise samples with different seed offsets, combined as `caveValue = n1² + n2²`. A voxel is carved when `caveValue < adjustedThreshold`.

Depth factor makes caves larger underground:
```
depthFactor = 1.0 + 0.5 * saturate((SeaLevel - worldY) / SeaLevel)
adjustedThreshold = CaveThreshold * depthFactor
```
At sea level, `depthFactor = 1.0`. At `worldY = 0`, `depthFactor = 1.5` (caves up to 50% larger).

Guards: carving skips Y < `MinCarveY`, skips a buffer zone near sea level (`SeaLevelCarveBuffer`), and never carves air or water blocks.

### Generation Scheduling — Forward-Weighted Priority

`GenerationScheduler.ScheduleJobs()` gets candidates from `ChunkManager.FillChunksToGenerate()`, which pops from a queue pre-sorted by `dist² * (2 - dot(direction, cameraForward))` in `UpdateLoadingQueue`. This ensures chunks in the camera's forward direction generate before those behind or to the side.

If a saved chunk exists in `WorldStorage`, it loads synchronously (no job scheduled) and transitions directly to `Generated`. Otherwise, the 7-stage Burst job chain runs on a worker thread.

---

## Biome System

### NativeBiomeData (Blittable)

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct NativeBiomeData
{
    public byte BiomeId;
    public float TemperatureMin;
    public float TemperatureMax;
    public float TemperatureCenter;     // for distance-based selection
    public float HumidityMin;
    public float HumidityMax;
    public float HumidityCenter;

    public StateId TopBlock;            // grass_block, sand, snow
    public StateId FillerBlock;         // dirt, sandstone
    public StateId StoneBlock;          // stone, deepslate
    public StateId UnderwaterBlock;     // gravel, sand
    public byte FillerDepth;            // how many layers of filler
    public float TreeDensity;           // probability per column (0.0 = no trees)
    public float HeightModifier;        // terrain height adjustment
    public byte TreeTemplateIndex;      // 0=Oak, 1=Birch, 2=Spruce
}
```

### Biome Assignment (Burst Job)

```csharp
[BurstCompile]
public struct BiomeAssignmentJob : IJob
{
    [ReadOnly] public NativeArray<int> HeightMap;
    [ReadOnly] public NativeArray<NativeBiomeData> BiomeData;
    [ReadOnly] public long Seed;
    [ReadOnly] public int3 ChunkCoord;

    public NativeArray<byte> BiomeMap;
    public NativeArray<float> TemperatureMap;
    public NativeArray<float> HumidityMap;

    public void Execute()
    {
        int wx = ChunkCoord.x * ChunkConstants.Size;
        int wz = ChunkCoord.z * ChunkConstants.Size;

        for (int x = 0; x < ChunkConstants.Size; x++)
        {
            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                int idx = z * ChunkConstants.Size + x;
                float temp = NativeNoise.Sample2D(wx + x, wz + z, TEMP_NOISE_CONFIG, Seed);
                float humid = NativeNoise.Sample2D(wx + x, wz + z, HUMID_NOISE_CONFIG, Seed + 999);

                TemperatureMap[idx] = temp;
                HumidityMap[idx] = humid;

                // Find closest biome
                byte bestBiome = 0;
                float bestDist = float.MaxValue;

                for (int b = 0; b < BiomeData.Length; b++)
                {
                    NativeBiomeData bd = BiomeData[b];
                    if (temp >= bd.TemperatureMin && temp <= bd.TemperatureMax
                        && humid >= bd.HumidityMin && humid <= bd.HumidityMax)
                    {
                        float dist = math.distancesq(
                            new float2(temp, humid),
                            new float2(bd.TemperatureCenter, bd.HumidityCenter));
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestBiome = bd.BiomeId;
                        }
                    }
                }

                BiomeMap[idx] = bestBiome;
            }
        }
    }
}
```

---

## Invariants

1. Each generation stage receives exclusive write access to the chunk's NativeArray. No two stages run concurrently on the same chunk.
2. Stages execute in priority order via `JobHandle` dependency chain.
3. A stage may read `HeightMap` only if `TerrainShapeJob` has completed (guaranteed by dependency chain).
4. A stage may read `BiomeMap` only if `BiomeAssignmentJob` has completed.
5. WorldGen output is deterministic given the same seed and registered content definitions.
6. No stage allocates NativeContainers with `Allocator.Persistent` — all per-generation allocations use `Allocator.TempJob`.
7. The `GenerationHandle` caller is responsible for `Dispose()` of all temporary NativeArrays after job completion.
8. Decoration overflow (cross-chunk blocks) is handled via managed `PendingDecorationStore`, never via Burst.
9. `BiomeData[i].BiomeId == i` — biome lookup is O(1) by direct index. Both `SurfaceBuilderJob.GetBiome()` and `DecorationStage.GetBiome()` rely on this.
10. `DecorationStage` runs on the main thread after the Burst job chain completes, invoked by `GenerationScheduler`.
