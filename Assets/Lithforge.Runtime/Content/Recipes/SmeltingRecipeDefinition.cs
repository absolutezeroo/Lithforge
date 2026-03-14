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
        [FormerlySerializedAs("inputItemId")]
        [Tooltip("Input item resource ID (e.g. lithforge:iron_ore)")]
        [SerializeField] private string _inputItemId = "";

        [FormerlySerializedAs("resultItemId")]
        [Tooltip("Result item resource ID (e.g. lithforge:iron_ingot)")]
        [SerializeField] private string _resultItemId = "";

        [FormerlySerializedAs("resultCount")]
        [Tooltip("Number of result items per smelt")]
        [Min(1)]
        [SerializeField] private int _resultCount = 1;

        [FormerlySerializedAs("experienceReward")]
        [Tooltip("Experience reward per smelt")]
        [Min(0f)]
        [SerializeField] private float _experienceReward;

        public string InputItemId
        {
            get { return _inputItemId; }
        }

        public string ResultItemId
        {
            get { return _resultItemId; }
        }

        public int ResultCount
        {
            get { return _resultCount; }
        }

        public float ExperienceReward
        {
            get { return _experienceReward; }
        }
    }
}
