using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    /// <summary>
    /// Serializable condition attached to a loot pool or entry. Evaluated at resolve time to gate
    /// whether the pool rolls or the entry is included (e.g. "silk_touch", "random_chance").
    /// </summary>
    [System.Serializable]
    public sealed class LootConditionEntry
    {
        /// <summary>Condition identifier (e.g. "random_chance", "silk_touch", "match_tool").</summary>
        [FormerlySerializedAs("_conditionType"),Tooltip("Condition type")]
        [SerializeField] private string conditionType = "";

        /// <summary>Key-value parameters interpreted by the condition evaluator (e.g. "chance"="0.5").</summary>
        [FormerlySerializedAs("_parameters"),Tooltip("Condition parameters as key=value pairs")]
        [SerializeField] private List<StringPair> parameters = new();

        /// <summary>Condition identifier (e.g. "random_chance", "silk_touch", "match_tool").</summary>
        public string ConditionType
        {
            get { return conditionType; }
        }

        /// <summary>Key-value parameters interpreted by the condition evaluator (e.g. "chance"="0.5").</summary>
        public IReadOnlyList<StringPair> Parameters
        {
            get { return parameters; }
        }
    }
}
