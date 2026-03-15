using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Behaviors
{
    /// <summary>
    /// Behavior action that spawns an entity (mob, NPC, item entity) near the block when triggered.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpawnEntityAction", menuName = "Lithforge/Behaviors/Spawn Entity")]
    public sealed class SpawnEntityAction : BehaviorAction
    {
        /// <summary>Entity identifier to look up the prefab or factory. Reserved for future ECS integration.</summary>
        [FormerlySerializedAs("_entityId"),Tooltip("Entity prefab or id to spawn")]
        [SerializeField] private string entityId;

        /// <summary>World-space offset from the block origin where the entity appears.</summary>
        [FormerlySerializedAs("_spawnOffset"),Tooltip("Spawn offset from block position")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0.5f, 1.0f, 0.5f);

        /// <summary>Entity identifier to look up the prefab or factory.</summary>
        public string EntityId
        {
            get { return entityId; }
        }

        /// <summary>World-space offset from the block origin where the entity appears.</summary>
        public Vector3 SpawnOffset
        {
            get { return spawnOffset; }
        }
    }
}
