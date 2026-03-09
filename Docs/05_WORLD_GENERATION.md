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
├── DecorationStage        managed         priority: 500  (cross-chunk structures)
├── StructureStage         managed         priority: 600  (villages, dungeons)
└── InitialLightingJob     [BurstCompile]  priority: 700
```

### Why Stages Are Not All Burst Jobs

Most stages are Burst-compiled `IJob` structs that operate on NativeArrays. Two exceptions:

- **DecorationStage**: Tree/structure placement may cross chunk boundaries, requiring writes to the `ConcurrentDictionary<int3, PendingDecorations>` (managed collection). The stage schedules a Burst job for the actual block placement within the chunk, but the cross-boundary coordination is managed.
- **StructureStage**: Large multi-chunk structures (villages, dungeons) require complex layout logic with managed collections. Block placement within each chunk section is Burst-compiled.

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

```csharp
[BurstCompile]
public struct TerrainShapeJob : IJob
{
    // Outputs
    public NativeArray<StateId> ChunkData;
    public NativeArray<int> HeightMap;

    // Inputs
    [ReadOnly] public long Seed;
    [ReadOnly] public int3 ChunkCoord;
    [ReadOnly] public NativeNoiseConfig NoiseConfig;
    [ReadOnly] public int SeaLevel;
    [ReadOnly] public StateId StoneId;
    [ReadOnly] public StateId WaterId;
    [ReadOnly] public StateId AirId;

    public void Execute()
    {
        int worldX = ChunkCoord.x * ChunkConstants.Size;
        int worldY = ChunkCoord.y * ChunkConstants.Size;
        int worldZ = ChunkCoord.z * ChunkConstants.Size;

        for (int x = 0; x < ChunkConstants.Size; x++)
        {
            for (int z = 0; z < ChunkConstants.Size; z++)
            {
                float noiseVal = NativeNoise.Sample2D(
                    worldX + x, worldZ + z, NoiseConfig, Seed);
                int surfaceY = SeaLevel + (int)(noiseVal * NoiseConfig.HeightScale);
                HeightMap[z * ChunkConstants.Size + x] = surfaceY;

                for (int y = 0; y < ChunkConstants.Size; y++)
                {
                    int wy = worldY + y;
                    int index = ChunkData_GetIndex(x, y, z);

                    if (wy <= surfaceY)
                        ChunkData[index] = StoneId;
                    else if (wy <= SeaLevel)
                        ChunkData[index] = WaterId;
                    else
                        ChunkData[index] = AirId;
                }
            }
        }
    }
}
```

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

All 6 ore types from Luanti, implemented as Burst jobs:

| Type | Algorithm | Burst | Parameters |
|------|-----------|-------|------------|
| Scatter | Random positions at configured density | Yes | density, y_range |
| Blob | Spheroid clusters at random centers | Yes | cluster_size, y_range |
| Vein | 3D noise-guided branching paths | Yes | noise_config, thickness |
| Sheet | Thin horizontal layer | Yes | thickness, y_level, noise |
| Stratum | Continuous horizontal band | Yes | thickness, y_level |
| Puff | Noise-shaped 3D deposits | Yes | noise_config, threshold |

### Cross-Chunk Decoration Handling

Trees and structures that cross chunk boundaries use a **pending decorations** system:

```
1. During DecorationStage for chunk C:
   - For each decoration to place:
     - Blocks within C → write directly to ChunkData
     - Blocks outside C → store in ConcurrentDictionary<int3, List<PendingBlock>>

2. When neighbor chunk N is generated:
   - After N's own DecorationStage:
   - Check ConcurrentDictionary for pending blocks targeting N
   - Apply pending blocks to N's ChunkData
   - Remove consumed entries

3. ConcurrentDictionary is managed code (not Burst).
   The actual block writes are simple StateId assignments.
```

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
    public float HumidityMin;
    public float HumidityMax;
    public float TemperatureCenter;     // for distance-based selection
    public float HumidityCenter;

    public StateId TopBlock;            // grass_block, sand, snow
    public StateId FillerBlock;         // dirt, sandstone
    public StateId StoneBlock;          // stone, deepslate
    public StateId UnderwaterBlock;     // gravel, sand
    public byte FillerDepth;            // how many layers of filler
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
8. Decoration overflow (cross-chunk blocks) is handled via managed `ConcurrentDictionary`, never via Burst.
