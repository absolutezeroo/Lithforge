using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    [System.Serializable]
    public sealed class BlockPropertyEntry
    {
        [FormerlySerializedAs("_name"),Tooltip("Property name (e.g. 'facing', 'lit', 'axis')")]
        [SerializeField] private string name;

        [FormerlySerializedAs("_kind"),Tooltip("Property type")]
        [SerializeField] private BlockPropertyKind kind;

        [FormerlySerializedAs("_values"),Tooltip("Possible values (for enum type, or auto-generated for bool/int)")]
        [SerializeField] private List<string> values = new List<string>();

        [FormerlySerializedAs("_defaultValue"),Tooltip("Default value")]
        [SerializeField] private string defaultValue;

        [FormerlySerializedAs("_minValue"),Tooltip("Min value (for int range type)")]
        [SerializeField] private int minValue;

        [FormerlySerializedAs("_maxValue"),Tooltip("Max value (for int range type)")]
        [SerializeField] private int maxValue;

        public string Name
        {
            get { return name; }
        }

        public BlockPropertyKind Kind
        {
            get { return kind; }
        }

        public IReadOnlyList<string> Values
        {
            get { return values; }
        }

        public string DefaultValue
        {
            get { return defaultValue; }
        }

        public int MinValue
        {
            get { return minValue; }
        }

        public int MaxValue
        {
            get { return maxValue; }
        }

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
