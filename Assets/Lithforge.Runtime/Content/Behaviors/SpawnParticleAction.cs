using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    /// <summary>
    ///     Behavior action that instantiates a particle effect at the block's position when triggered.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpawnParticleAction", menuName = "Lithforge/Behaviors/Spawn Particle")]
    public sealed class SpawnParticleAction : BehaviorAction
    {
        /// <summary>Particle system prefab to instantiate. Destroyed automatically when emission ends.</summary>
        [FormerlySerializedAs("_particlePrefab"), Tooltip("Particle system prefab"), SerializeField]
         private ParticleSystem particlePrefab;

        /// <summary>Offset from the block's origin to spawn the particle effect.</summary>
        [FormerlySerializedAs("_spawnOffset"), Tooltip("Spawn offset from block center"), SerializeField]
         private Vector3 spawnOffset = new(0.5f, 0.5f, 0.5f);

        /// <summary>Particle system prefab to instantiate.</summary>
        public ParticleSystem ParticlePrefab
        {
            get { return particlePrefab; }
        }

        /// <summary>Offset from the block's origin to spawn the particle effect.</summary>
        public Vector3 SpawnOffset
        {
            get { return spawnOffset; }
        }
    }
}
