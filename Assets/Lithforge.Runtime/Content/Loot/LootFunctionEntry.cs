using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class LootFunctionEntry
    {
        [FormerlySerializedAs("functionType")]
        [Tooltip("Function type")]
        [SerializeField] private string _functionType = "";

        [FormerlySerializedAs("parameters")]
        [Tooltip("Function parameters as key=value pairs")]
        [SerializeField] private List<StringPair> _parameters = new List<StringPair>();

        public string FunctionType
        {
            get { return _functionType; }
        }

        public IReadOnlyList<StringPair> Parameters
        {
            get { return _parameters; }
        }
    }
}
