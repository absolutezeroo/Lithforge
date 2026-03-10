using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class LootConditionEntry
    {
        [Tooltip("Condition type")]
        [SerializeField] private string conditionType = "";

        [Tooltip("Condition parameters as key=value pairs")]
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
