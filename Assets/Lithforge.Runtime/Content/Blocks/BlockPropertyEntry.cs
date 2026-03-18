using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    /// <summary>
    /// Defines a single block state property (e.g. "facing", "lit", "axis") and its valid values.
    /// Used by ContentPipeline to expand the cartesian product of all properties into discrete block states.
    /// </summary>
    [System.Serializable]
    public sealed class BlockPropertyEntry
    {
        /// <summary>Property name used in state keys (e.g. "facing", "axis", "lit").</summary>
        [FormerlySerializedAs("_name"),Tooltip("Property name (e.g. 'facing', 'lit', 'axis')")]
        [SerializeField] private string name;

        /// <summary>How this property generates its value set (bool, int range, or explicit enum).</summary>
        [FormerlySerializedAs("_kind"),Tooltip("Property type")]
        [SerializeField] private BlockPropertyKind kind;

        /// <summary>Explicit value list — only used when Kind is Enum.</summary>
        [FormerlySerializedAs("_values"),Tooltip("Possible values (for enum type, or auto-generated for bool/int)")]
        [SerializeField] private List<string> values = new();

        /// <summary>Value assigned when no property string is specified in a block state.</summary>
        [FormerlySerializedAs("_defaultValue"),Tooltip("Default value")]
        [SerializeField] private string defaultValue;

        /// <summary>Inclusive lower bound for IntRange properties.</summary>
        [FormerlySerializedAs("_minValue"),Tooltip("Min value (for int range type)")]
        [SerializeField] private int minValue;

        /// <summary>Inclusive upper bound for IntRange properties.</summary>
        [FormerlySerializedAs("_maxValue"),Tooltip("Max value (for int range type)")]
        [SerializeField] private int maxValue;

        /// <summary>Property name used in state keys (e.g. "facing", "axis", "lit").</summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>How this property generates its value set (bool, int range, or explicit enum).</summary>
        public BlockPropertyKind Kind
        {
            get { return kind; }
        }

        /// <summary>Explicit value list — only used when Kind is Enum.</summary>
        public IReadOnlyList<string> Values
        {
            get { return values; }
        }

        /// <summary>Value assigned when no property string is specified in a block state.</summary>
        public string DefaultValue
        {
            get { return defaultValue; }
        }

        /// <summary>Inclusive lower bound for IntRange properties.</summary>
        public int MinValue
        {
            get { return minValue; }
        }

        /// <summary>Inclusive upper bound for IntRange properties.</summary>
        public int MaxValue
        {
            get { return maxValue; }
        }

        /// <summary>Total number of valid values — used for cartesian product calculation.</summary>
        public int ValueCount
        {
            get
            {
                return kind switch
                {
                    BlockPropertyKind.Bool => 2,
                    BlockPropertyKind.IntRange => maxValue - minValue + 1,
                    BlockPropertyKind.Enum => values.Count,
                    _ => 1,
                };
            }
        }

        /// <summary>
        /// Returns the property value at the given index within the value range.
        /// For Bool: 0="true", 1="false". For IntRange: min+index. For Enum: values[index].
        /// </summary>
        /// <param name="index">Zero-based index into the property's value range.</param>
        /// <returns>String representation of the value at this index.</returns>
        public string GetValue(int index)
        {
            return kind switch
            {
                BlockPropertyKind.Bool => index == 0 ? "true" : "false",
                BlockPropertyKind.IntRange => (minValue + index).ToString(),
                BlockPropertyKind.Enum => values[index],
                _ => defaultValue,
            };
        }
    }
}
