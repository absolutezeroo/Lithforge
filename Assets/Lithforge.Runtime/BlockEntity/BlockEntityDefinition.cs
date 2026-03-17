using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.BlockEntity
{
    /// <summary>
    /// ScriptableObject that links a block (by namespace:name) to a block entity type ID.
    /// Loaded in ContentPipeline Phase 2.5 to patch FlagHasBlockEntity on the state registry.
    ///
    /// Create assets via: Assets > Create > Lithforge > Content > Block Entity Definition
    /// Place in: Assets/Resources/Content/BlockEntities/
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewBlockEntity",
        menuName = "Lithforge/Content/Block Entity Definition",
        order = 10)]
    public sealed class BlockEntityDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_namespace"),Tooltip("Namespace for the block resource id")]
        [SerializeField] private string @namespace = "lithforge";

        [FormerlySerializedAs("_blockName"),Tooltip("Block name (must match a registered block)")]
        [SerializeField] private string blockName = "";

        [FormerlySerializedAs("_blockEntityTypeId"),Tooltip("Block entity type ID (e.g. lithforge:chest)")]
        [SerializeField] private string blockEntityTypeId = "";

        [Tooltip("Screen type ID used to open this block entity's UI (e.g. lithforge:chest_screen). " +
                 "Must match the entityTypeId registered on the ContainerScreenManager.")]
        [SerializeField] private string screenTypeId = "";

        public string Namespace
        {
            get { return @namespace; }
        }

        public string BlockName
        {
            get { return blockName; }
        }

        public string BlockEntityTypeId
        {
            get { return blockEntityTypeId; }
        }

        /// <summary>
        /// Screen type ID for the container screen associated with this block entity.
        /// Must match the entityTypeId registered on the ContainerScreenManager.
        /// </summary>
        public string ScreenTypeId
        {
            get { return screenTypeId; }
        }

        /// <summary>
        /// Returns the full block ID string (namespace:name).
        /// </summary>
        public string BlockIdString
        {
            get { return @namespace + ":" + blockName; }
        }
    }
}
