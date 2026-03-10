using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class LootFunctionEntry
    {
        [Tooltip("Function type")]
        [SerializeField] private string functionType = "";

        [Tooltip("Function parameters as key=value pairs")]
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
