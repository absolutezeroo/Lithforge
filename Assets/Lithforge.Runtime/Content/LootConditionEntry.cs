using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class LootConditionEntry
    {
        [Tooltip("Condition type")]
        [SerializeField] private string _conditionType = "";

        [Tooltip("Condition parameters as key=value pairs")]
        [SerializeField] private List<StringPair> _parameters = new List<StringPair>();

        public string ConditionType
        {
            get { return _conditionType; }
        }

        public IReadOnlyList<StringPair> Parameters
        {
            get { return _parameters; }
        }
    }
}
