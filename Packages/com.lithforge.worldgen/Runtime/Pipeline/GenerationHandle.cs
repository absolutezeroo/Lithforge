using System;
using Lithforge.WorldGen.Climate;
using Lithforge.WorldGen.Lighting;
using Lithforge.WorldGen.Stages;
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

        public void Dispose()
        {
            // HeightMap is not disposed here — it is transferred to ManagedChunk
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
        }
    }
}
