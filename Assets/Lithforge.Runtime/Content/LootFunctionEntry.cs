using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class LootFunctionEntry
    {
        [Tooltip("Function type")]
        [SerializeField] private string _functionType = "";

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
