using UnityEngine;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewSpawnParticleAction", menuName = "Lithforge/Behaviors/Spawn Particle")]
    public sealed class SpawnParticleAction : BehaviorAction
    {
        [Tooltip("Particle system prefab")]
        [SerializeField] private ParticleSystem particlePrefab;

        [Tooltip("Spawn offset from block center")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0.5f, 0.5f, 0.5f);

        public ParticleSystem ParticlePrefab
        {
            get { return particlePrefab; }
        }

        public Vector3 SpawnOffset
        {
            get { return spawnOffset; }
        }
    }
}
