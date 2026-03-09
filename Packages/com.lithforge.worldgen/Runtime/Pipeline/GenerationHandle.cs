using System;
using Unity.Collections;
using Unity.Jobs;

namespace Lithforge.WorldGen.Pipeline
{
    public struct GenerationHandle : IDisposable
    {
        public JobHandle FinalHandle;
        public NativeArray<int> HeightMap;

        public void Dispose()
        {
            if (HeightMap.IsCreated)
            {
                HeightMap.Dispose();
            }
        }
    }
}
