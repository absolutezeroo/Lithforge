using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Blocks
{
    [System.Serializable]
    public sealed class BlockPropertyEntry
    {
        [FormerlySerializedAs("name")]
        [Tooltip("Property name (e.g. 'facing', 'lit', 'axis')")]
        [SerializeField] private string _name;

        [FormerlySerializedAs("kind")]
        [Tooltip("Property type")]
        [SerializeField] private BlockPropertyKind _kind;

        [FormerlySerializedAs("values")]
        [Tooltip("Possible values (for enum type, or auto-generated for bool/int)")]
        [SerializeField] private List<string> _values = new List<string>();

        [FormerlySerializedAs("defaultValue")]
        [Tooltip("Default value")]
        [SerializeField] private string _defaultValue;

        [FormerlySerializedAs("minValue")]
        [Tooltip("Min value (for int range type)")]
        [SerializeField] private int _minValue;

        [FormerlySerializedAs("maxValue")]
        [Tooltip("Max value (for int range type)")]
        [SerializeField] private int _maxValue;

        public string Name
        {
            get { return _name; }
        }

        public BlockPropertyKind Kind
        {
            get { return _kind; }
        }

        public IReadOnlyList<string> Values
        {
            get { return _values; }
        }

        public string DefaultValue
        {
            get { return _defaultValue; }
        }

        public int MinValue
        {
            get { return _minValue; }
        }

        public int MaxValue
        {
            get { return _maxValue; }
        }

        public int ValueCount
        {
            get
            {
                return _kind switch
                {
                    BlockPropertyKind.Bool => 2,
                    BlockPropertyKind.IntRange => _maxValue - _minValue + 1,
                    BlockPropertyKind.Enum => _values.Count,
                    _ => 1,
                };
            }
        }

        public string GetValue(int index)
        {
            return _kind switch
            {
                BlockPropertyKind.Bool => index == 0 ? "true" : "false",
                BlockPropertyKind.IntRange => (_minValue + index).ToString(),
                BlockPropertyKind.Enum => _values[index],
                _ => _defaultValue,
            };
        }
    }
}
