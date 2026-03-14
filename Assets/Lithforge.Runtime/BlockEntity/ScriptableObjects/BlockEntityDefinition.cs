using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.BlockEntity.ScriptableObjects
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

        [FormerlySerializedAs("blockName")]
        [Tooltip("Block name (must match a registered block)")]
        [SerializeField] private string _blockName = "";

        [FormerlySerializedAs("blockEntityTypeId")]
        [Tooltip("Block entity type ID (e.g. lithforge:chest)")]
        [SerializeField] private string _blockEntityTypeId = "";

        public string Namespace
        {
            get { return @namespace; }
        }

        public string BlockName
        {
            get { return _blockName; }
        }

        public string BlockEntityTypeId
        {
            get { return _blockEntityTypeId; }
        }

        /// <summary>
        /// Returns the full block ID string (namespace:name).
        /// </summary>
        public string BlockIdString
        {
            get { return @namespace + ":" + _blockName; }
        }
    }
}
