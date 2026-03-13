using System;
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
        public NativeArray<float> TemperatureMap;
        public NativeArray<float> HumidityMap;

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

            if (TemperatureMap.IsCreated)
            {
                TemperatureMap.Dispose();
            }

            if (HumidityMap.IsCreated)
            {
                HumidityMap.Dispose();
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

            if (TemperatureMap.IsCreated)
            {
                TemperatureMap.Dispose();
            }

            if (HumidityMap.IsCreated)
            {
                HumidityMap.Dispose();
            }

            if (BorderLightOutput.IsCreated)
            {
                BorderLightOutput.Dispose();
            }
        }
    }
}
