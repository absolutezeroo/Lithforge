using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    [CreateAssetMenu(fileName = "NewSpawnEntityAction", menuName = "Lithforge/Behaviors/Spawn Entity")]
    public sealed class SpawnEntityAction : BehaviorAction
    {
        [FormerlySerializedAs("_entityId"),Tooltip("Entity prefab or id to spawn")]
        [SerializeField] private string entityId;

        [FormerlySerializedAs("_spawnOffset"),Tooltip("Spawn offset from block position")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0.5f, 1.0f, 0.5f);

        public string EntityId
        {
            get { return entityId; }
        }

        public Vector3 SpawnOffset
        {
            get { return spawnOffset; }
        }
    }
}
