using UnityEngine;

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
        [Tooltip("Namespace for the block resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Block name (must match a registered block)")]
        [SerializeField] private string blockName = "";

        [Tooltip("Block entity type ID (e.g. lithforge:chest)")]
        [SerializeField] private string blockEntityTypeId = "";

        public string Namespace
        {
            get { return _namespace; }
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
        /// Returns the full block ID string (namespace:name).
        /// </summary>
        public string BlockIdString
        {
            get { return _namespace + ":" + blockName; }
        }
    }
}
