using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class LootConditionEntry
    {
        [FormerlySerializedAs("_conditionType"),Tooltip("Condition type")]
        [SerializeField] private string conditionType = "";

        [FormerlySerializedAs("_parameters"),Tooltip("Condition parameters as key=value pairs")]
        [SerializeField] private List<StringPair> parameters = new List<StringPair>();

        public string ConditionType
        {
            get { return conditionType; }
        }

        public IReadOnlyList<StringPair> Parameters
        {
            get { return parameters; }
        }
    }
}
