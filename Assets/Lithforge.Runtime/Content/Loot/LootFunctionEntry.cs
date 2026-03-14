using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class LootFunctionEntry
    {
        [FormerlySerializedAs("_functionType"),Tooltip("Function type")]
        [SerializeField] private string functionType = "";

        [FormerlySerializedAs("_parameters"),Tooltip("Function parameters as key=value pairs")]
        [SerializeField] private List<StringPair> parameters = new List<StringPair>();

        public string FunctionType
        {
            get { return functionType; }
        }

        public IReadOnlyList<StringPair> Parameters
        {
            get { return parameters; }
        }
    }
}
