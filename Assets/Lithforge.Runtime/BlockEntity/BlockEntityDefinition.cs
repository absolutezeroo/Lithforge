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
        /// <summary>Namespace for the block resource ID (e.g. "lithforge").</summary>
        [FormerlySerializedAs("_namespace"),Tooltip("Namespace for the block resource id")]
        [SerializeField] private string @namespace = "lithforge";

        /// <summary>Block name that must match a registered block definition.</summary>
        [FormerlySerializedAs("_blockName"),Tooltip("Block name (must match a registered block)")]
        [SerializeField] private string blockName = "";

        /// <summary>Unique type ID for the block entity (e.g. "lithforge:chest").</summary>
        [FormerlySerializedAs("_blockEntityTypeId"),Tooltip("Block entity type ID (e.g. lithforge:chest)")]
        [SerializeField] private string blockEntityTypeId = "";

        /// <summary>Screen type ID for the container screen UI associated with this block entity.</summary>
        [Tooltip("Screen type ID used to open this block entity's UI (e.g. lithforge:chest_screen). " +
                 "Must match the entityTypeId registered on the ContainerScreenManager.")]
        [SerializeField] private string screenTypeId = "";

        /// <summary>Gets the namespace portion of the block resource ID.</summary>
        public string Namespace
        {
            get { return @namespace; }
        }

        /// <summary>Gets the block name portion of the block resource ID.</summary>
        public string BlockName
        {
            get { return blockName; }
        }

        /// <summary>Gets the unique block entity type identifier.</summary>
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
