using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    /// <summary>
    ///     Serializable function that modifies a loot drop after selection (e.g. set count, apply fortune bonus).
    ///     Parameters are pre-parsed at load time to avoid per-resolve string parsing.
    /// </summary>
    [Serializable]
    public sealed class LootFunctionEntry
    {
        /// <summary>Function identifier (e.g. "set_count", "apply_bonus", "explosion_decay").</summary>
        [FormerlySerializedAs("_functionType"), Tooltip("Function type"), SerializeField]
         private string functionType = "";

        /// <summary>Key-value parameters interpreted by the function (e.g. "min"="1", "max"="3").</summary>
        [FormerlySerializedAs("_parameters"), Tooltip("Function parameters as key=value pairs"), SerializeField]
         private List<StringPair> parameters = new();

        /// <summary>Function identifier (e.g. "set_count", "apply_bonus", "explosion_decay").</summary>
        public string FunctionType
        {
            get { return functionType; }
        }

        /// <summary>Key-value parameters interpreted by the function (e.g. "min"="1", "max"="3").</summary>
        public IReadOnlyList<StringPair> Parameters
        {
            get { return parameters; }
        }
    }
}
