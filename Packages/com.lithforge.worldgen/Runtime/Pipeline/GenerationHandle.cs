using System;
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

        public void Dispose()
        {
            if (HeightMap.IsCreated)
            {
                HeightMap.Dispose();
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
        }
    }
}
