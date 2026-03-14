using System;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Lighting;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.WorldGen.Pipeline
{
    public struct GenerationHandle : IDisposable
    {
        public JobHandle FinalHandle;
        public NativeArray<int> HeightMap;
        public NativeArray<byte> LightData;
        public NativeArray<byte> BiomeMap;

        /// <summary>
        /// Per-column climate values produced by ClimateNoiseJob.
        /// Owner: GenerationHandle (allocated in GenerationPipeline.Schedule).
        /// Dispose: GenerationHandle.Dispose after decoration consumes it.
        /// </summary>
        public NativeArray<ClimateData> ClimateMap;

        /// <summary>
        /// Border light entries collected by LightPropagationJob for cross-chunk propagation.
        /// Owner: GenerationHandle (allocated in GenerationPipeline.Schedule).
        /// Dispose: GenerationScheduler.PollCompleted() after reading entries.
        /// </summary>
        public NativeList<NativeBorderLightEntry> BorderLightOutput;

        /// <summary>
        /// Per-column river presence flags produced by RiverNoiseJob.
        /// 0 = no river, 1 = river column.
        /// Owner: GenerationHandle (allocated in GenerationPipeline.Schedule).
        /// Dispose: NOT disposed in Dispose() — transferred to ManagedChunk on completion.
        ///          Disposed in DisposeAll() for cancelled generation.
        /// </summary>
        public NativeArray<byte> RiverFlags;

        /// <summary>
        /// Per-column river carve depth produced by RiverNoiseJob, consumed by RiverCarveJob.
        /// Transient — not needed after generation completes.
        /// Owner: GenerationHandle (allocated in GenerationPipeline.Schedule).
        /// Dispose: GenerationHandle.Dispose after RiverCarveJob consumes it.
        /// </summary>
        public NativeArray<float> RiverCarveDepth;

        public void Dispose()
        {
            // HeightMap is not disposed here — it is transferred to ManagedChunk
            // when the generation completes. Only dispose if the generation was cancelled (DisposeAll).

            // RiverFlags is not disposed here — it is transferred to ManagedChunk
            // when the generation completes. Only dispose if the generation was cancelled (DisposeAll).

            if (BiomeMap.IsCreated)
            {
                BiomeMap.Dispose();
            }

            if (ClimateMap.IsCreated)
            {
                ClimateMap.Dispose();
            }

            if (BorderLightOutput.IsCreated)
            {
                BorderLightOutput.Dispose();
            }

            if (RiverCarveDepth.IsCreated)
            {
                RiverCarveDepth.Dispose();
            }

            // LightData is not disposed here — it is transferred to ManagedChunk
            // when the generation completes. Only dispose if the generation was cancelled.
        }

        public void DisposeAll()
        {
            if (HeightMap.IsCreated)
            {
                HeightMap.Dispose();
            }

            if (LightData.IsCreated)
            {
                LightData.Dispose();
            }

            if (BiomeMap.IsCreated)
            {
                BiomeMap.Dispose();
            }

            if (ClimateMap.IsCreated)
            {
                ClimateMap.Dispose();
            }

            if (BorderLightOutput.IsCreated)
            {
                BorderLightOutput.Dispose();
            }

            if (RiverFlags.IsCreated)
            {
                RiverFlags.Dispose();
            }

            if (RiverCarveDepth.IsCreated)
            {
                RiverCarveDepth.Dispose();
            }
        }
    }
}
