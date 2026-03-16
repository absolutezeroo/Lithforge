using Lithforge.Physics;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Probes the environment around the player using DDA raycasts in
    /// fibonacci-sphere distributed directions to measure enclosure ratio.
    /// Used by <see cref="CaveReverbController"/> to drive reverb intensity.
    /// Updates at a configurable tick interval (~0.5s).
    /// </summary>
    public sealed class EnclosureProbe
    {
        private readonly System.Func<int3, bool> _isSolidDelegate;
        private readonly Transform _cameraTransform;
        private readonly int _rayCount;
        private readonly int _maxDistance;
        private readonly int _updateTicks;

        private readonly float3[] _directions;
        private int _tickCounter;
        private float _enclosureRatio;

        public float EnclosureRatio
        {
            get { return _enclosureRatio; }
        }

        public EnclosureProbe(
            System.Func<int3, bool> isSolidDelegate,
            Transform cameraTransform,
            int rayCount,
            int maxDistance,
            int updateTicks)
        {
            _isSolidDelegate = isSolidDelegate;
            _cameraTransform = cameraTransform;
            _rayCount = rayCount;
            _maxDistance = maxDistance;
            _updateTicks = updateTicks;

            _directions = GenerateFibonacciSphere(rayCount);
        }

        /// <summary>
        /// Called at 30 TPS. Re-evaluates enclosure every N ticks.
        /// </summary>
        public void Tick()
        {
            _tickCounter++;

            if (_tickCounter < _updateTicks)
            {
                return;
            }

            _tickCounter = 0;

            if (_cameraTransform == null)
            {
                return;
            }

            float3 origin = new float3(
                _cameraTransform.position.x,
                _cameraTransform.position.y,
                _cameraTransform.position.z);

            int hits = 0;

            for (int i = 0; i < _directions.Length; i++)
            {
                RaycastHit hit = VoxelRaycast.Cast(
                    origin, _directions[i], _maxDistance, _isSolidDelegate);

                if (hit.DidHit)
                {
                    hits++;
                }
            }

            _enclosureRatio = (float)hits / _rayCount;
        }

        /// <summary>
        /// Generates evenly distributed directions on a unit sphere using the
        /// Fibonacci lattice method.
        /// </summary>
        private static float3[] GenerateFibonacciSphere(int count)
        {
            float3[] dirs = new float3[count];
            float goldenAngle = math.PI * (3f - math.sqrt(5f));

            for (int i = 0; i < count; i++)
            {
                float y = 1f - ((float)i / (count - 1)) * 2f;
                float radius = math.sqrt(1f - y * y);
                float theta = goldenAngle * i;

                dirs[i] = new float3(
                    math.cos(theta) * radius,
                    y,
                    math.sin(theta) * radius);
            }

            return dirs;
        }
    }
}
