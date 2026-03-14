using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewSpawnParticleAction", menuName = "Lithforge/Behaviors/Spawn Particle")]
    public sealed class SpawnParticleAction : BehaviorAction
    {
        [FormerlySerializedAs("particlePrefab")]
        [Tooltip("Particle system prefab")]
        [SerializeField] private ParticleSystem _particlePrefab;

        [FormerlySerializedAs("spawnOffset")]
        [Tooltip("Spawn offset from block center")]
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0.5f, 0.5f, 0.5f);

        public ParticleSystem ParticlePrefab
        {
            get { return _particlePrefab; }
        }

        public Vector3 SpawnOffset
        {
            get { return _spawnOffset; }
        }
    }
}
