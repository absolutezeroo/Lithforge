using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class BlockPropertyEntry
    {
        [Tooltip("Property name (e.g. 'facing', 'lit', 'axis')")]
        [SerializeField] private string _name;

        [Tooltip("Property type")]
        [SerializeField] private BlockPropertyKind _kind;

        [Tooltip("Possible values (for enum type, or auto-generated for bool/int)")]
        [SerializeField] private List<string> _values = new List<string>();

        [Tooltip("Default value")]
        [SerializeField] private string _defaultValue;

        [Tooltip("Min value (for int range type)")]
        [SerializeField] private int _minValue;

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
                switch (_kind)
                {
                    case BlockPropertyKind.Bool:
                        return 2;
                    case BlockPropertyKind.IntRange:
                        return _maxValue - _minValue + 1;
                    case BlockPropertyKind.Enum:
                        return _values.Count;
                    default:
                        return 1;
                }
            }
        }

        public string GetValue(int index)
        {
            switch (_kind)
            {
                case BlockPropertyKind.Bool:
                    return index == 0 ? "true" : "false";
                case BlockPropertyKind.IntRange:
                    return (_minValue + index).ToString();
                case BlockPropertyKind.Enum:
                    return _values[index];
                default:
                    return _defaultValue;
            }
        }
    }
}
