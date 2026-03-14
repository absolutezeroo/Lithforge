using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class LootConditionEntry
    {
        [FormerlySerializedAs("conditionType")]
        [Tooltip("Condition type")]
        [SerializeField] private string _conditionType = "";

        [FormerlySerializedAs("parameters")]
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
