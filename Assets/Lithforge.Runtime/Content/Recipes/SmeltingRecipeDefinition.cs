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
        [FormerlySerializedAs("_inputItemId"),Tooltip("Input item resource ID (e.g. lithforge:iron_ore)")]
        [SerializeField] private string inputItemId = "";

        [FormerlySerializedAs("_resultItemId"),Tooltip("Result item resource ID (e.g. lithforge:iron_ingot)")]
        [SerializeField] private string resultItemId = "";

        [FormerlySerializedAs("_resultCount"),Tooltip("Number of result items per smelt")]
        [Min(1)]
        [SerializeField] private int resultCount = 1;

        [FormerlySerializedAs("_experienceReward"),Tooltip("Experience reward per smelt")]
        [Min(0f)]
        [SerializeField] private float experienceReward;

        public string InputItemId
        {
            get { return inputItemId; }
        }

        public string ResultItemId
        {
            get { return resultItemId; }
        }

        public int ResultCount
        {
            get { return resultCount; }
        }

        public float ExperienceReward
        {
            get { return experienceReward; }
        }
    }
}
