using Unity.Mathematics;

namespace Lithforge.Runtime.Scheduling
{
    /// <summary>
    /// Derives all scheduling parameters from renderDistance.
    /// Single source of truth — no hardcoded constants elsewhere.
    /// </summary>
    public static class SchedulingConfig
    {
        public static int MaxGenerationsPerFrame(int rd)
        {
            return math.min(16, 4 + rd / 2);
        }

        public static int MaxMeshesPerFrame(int rd)
        {
            return math.min(16, 4 + rd / 2);
        }

        public static int MaxGenCompletionsPerFrame(int rd)
        {
            return math.min(32, 4 + rd / 2);
        }

        public static int MaxMeshCompletionsPerFrame(int rd)
        {
            return math.min(32, 4 + rd / 2);
        }

        public static int MaxLODMeshesPerFrame(int rd)
        {
            return math.min(8, 2 + rd / 4);
        }

        public static int MaxLODCompletionsPerFrame(int rd)
        {
            return math.min(16, 2 + rd / 4);
        }

        public static int LOD1Distance(int rd)
        {
            return math.max(2, rd / 3);
        }

        public static int LOD2Distance(int rd)
        {
            return math.max(4, rd / 2);
        }

        public static int LOD3Distance(int rd)
        {
            return math.max(6, rd * 7 / 10);
        }

        public static int ThrottleThreshold(int rd)
        {
            return math.clamp(rd * 2, 16, 64);
        }
    }
}
