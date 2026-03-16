using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content.Tools
{
    /// <summary>
    /// Defines what a physical material (wood, stone, iron, etc.) contributes to a tool's stats
    /// when used for a specific part slot (head, handle, binding). Multiple parts composed from
    /// different materials combine to produce the final tool properties.
    /// </summary>
    [CreateAssetMenu(fileName = "NewToolMaterial",
        menuName = "Lithforge/Content/Tool Material Definition")]
    public sealed class ToolMaterialDefinition : ScriptableObject
    {
        /// <summary>Unique string key for this material (e.g. "iron", "diamond").</summary>
        [Header("Identity")]
        public string materialId;

        /// <summary>Which tool part slots this material can fill (head, handle, binding, etc.).</summary>
        [Header("Compatible Parts")]
        public ToolPartType[] compatibleParts;

        /// <summary>Base mining speed multiplier when this material is the tool head.</summary>
        [Header("Head / Blade Stats")]
        [Min(0.1f)] public float headMiningSpeed = 1f;

        /// <summary>Total durability contributed by a head made of this material.</summary>
        [Min(1)] public int headDurability = 100;

        /// <summary>Base melee damage contributed by a head or blade of this material.</summary>
        [Min(0f)] public float headAttackDamage = 1f;

        /// <summary>Multiplier applied to the tool's total durability when this material is the handle.</summary>
        [Header("Handle Stats")]
        [Min(0.1f)] public float handleDurabilityMultiplier = 1f;

        /// <summary>Multiplier applied to mining speed when this material is the handle.</summary>
        [Min(0.1f)] public float handleSpeedMultiplier = 1f;

        /// <summary>Flat durability bonus added when this material is used as a binding or guard.</summary>
        [Header("Binding / Guard Bonus")]
        [Min(0)] public int bindingDurabilityBonus = 0;

        /// <summary>Special trait identifiers granted by this material (e.g. "magnetic", "ecological").</summary>
        [Header("Traits")]
        public string[] traitIds = System.Array.Empty<string>();

        /// <summary>
        /// Harvest tier that determines which blocks this material can mine.
        /// Higher levels can break harder blocks (0 = wood, 1 = stone, 2 = iron, 3 = diamond).
        /// </summary>
        [Header("Tool Level")]
        [Min(0)] public int toolLevel = 0;

        /// <summary>
        /// If true, this material can be used in the Part Builder (wood, stone, flint...).
        /// If false, parts must be made via Smeltery/Casting (iron, gold, diamond...).
        /// </summary>
        [Header("Part Builder")]
        [Tooltip("If true, this material can be used in the Part Builder (wood, stone, flint...). " +
                 "If false, parts must be made via Smeltery/Casting (iron, gold, diamond...).")]
        public bool isCraftable = true;

        /// <summary>
        /// Material cost in items to craft one part in the Part Builder.
        /// Individual recipes can override this via costOverride.
        /// </summary>
        [Tooltip("Material cost in items to craft one part in the Part Builder. " +
                 "Individual recipes can override this via costOverride.")]
        [Min(1)] public int partBuilderCost = 2;

        /// <summary>
        /// Item IDs accepted as material input in the Part Builder.
        /// E.g. "lithforge:oak_planks" for wood, "lithforge:cobblestone" for stone.
        /// </summary>
        [Tooltip("Item IDs accepted as material input in the Part Builder. " +
                 "E.g. 'lithforge:oak_planks' for wood, 'lithforge:cobblestone' for stone.")]
        public string[] craftingItemIds = System.Array.Empty<string>();

        /// <summary>
        /// Suffix used in texture filenames (e.g. "iron", "wood").
        /// Must match the part after the underscore in texture names like "head_iron.png".
        /// If empty, defaults to the Name part of materialId.
        /// </summary>
        [Header("Sprite Compositing")]
        [Tooltip("Suffix used in texture filenames (e.g. 'iron', 'wood'). " +
                 "Must match the part after the underscore in texture names like 'head_iron.png'. " +
                 "If empty, defaults to the Name part of materialId.")]
        public string textureSuffix;

        /// <summary>
        /// If true, this material's textureSuffix is used as the fallback
        /// when another material has no matching texture.
        /// </summary>
        [Tooltip("If true, this material's textureSuffix is used as the fallback " +
                 "when another material has no matching texture.")]
        public bool isFallbackMaterial;
    }
}
