using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Recipes
{
    /// <summary>
    /// ScriptableObject defining a smelting recipe: one input item → one output item.
    /// Loaded by ContentPipeline Phase 16 and registered in SmeltingRecipeRegistry.
    ///
    /// Create assets via: Assets > Create > Lithforge > Content > Smelting Recipe
    /// Place in: Assets/Resources/Content/Recipes/Smelting/
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewSmeltingRecipe",
        menuName = "Lithforge/Content/Smelting Recipe",
        order = 5)]
    public sealed class SmeltingRecipeDefinition : ScriptableObject
    {
        /// <summary>ResourceId of the item consumed by smelting (e.g. "lithforge:iron_ore").</summary>
        [FormerlySerializedAs("_inputItemId"),Tooltip("Input item resource ID (e.g. lithforge:iron_ore)")]
        [SerializeField] private string inputItemId = "";

        /// <summary>ResourceId of the item produced by smelting (e.g. "lithforge:iron_ingot").</summary>
        [FormerlySerializedAs("_resultItemId"),Tooltip("Result item resource ID (e.g. lithforge:iron_ingot)")]
        [SerializeField] private string resultItemId = "";

        /// <summary>Number of result items produced per smelt operation.</summary>
        [FormerlySerializedAs("_resultCount"),Tooltip("Number of result items per smelt")]
        [Min(1)]
        [SerializeField] private int resultCount = 1;

        /// <summary>Experience points awarded to the player per smelt operation.</summary>
        [FormerlySerializedAs("_experienceReward"),Tooltip("Experience reward per smelt")]
        [Min(0f)]
        [SerializeField] private float experienceReward;

        /// <summary>ResourceId of the item consumed by smelting.</summary>
        public string InputItemId
        {
            get { return inputItemId; }
        }

        /// <summary>ResourceId of the item produced by smelting.</summary>
        public string ResultItemId
        {
            get { return resultItemId; }
        }

        /// <summary>Number of result items per smelt operation.</summary>
        public int ResultCount
        {
            get { return resultCount; }
        }

        /// <summary>Experience points awarded per smelt.</summary>
        public float ExperienceReward
        {
            get { return experienceReward; }
        }
    }
}
