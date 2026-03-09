using System;
using System.Collections.Generic;

namespace Lithforge.Core.Data
{
    /// <summary>
    /// Describes a block property and its possible values.
    /// Used to compute the cartesian product of block states.
    /// </summary>
    public sealed class PropertyDefinition
    {
        public string Name { get; }

        public PropertyKind Kind { get; }

        public IReadOnlyList<string> Values { get; }

        public string DefaultValue { get; }

        public int ValueCount
        {
            get { return Values.Count; }
        }

        private PropertyDefinition(string name, PropertyKind kind, IReadOnlyList<string> values, string defaultValue)
        {
            Name = name;
            Kind = kind;
            Values = values;
            DefaultValue = defaultValue;
        }

        public static PropertyDefinition Bool(string name, bool defaultValue)
        {
            List<string> values = new List<string> { "true", "false" };

            return new PropertyDefinition(name, PropertyKind.Bool, values, defaultValue.ToString().ToLowerInvariant());
        }

        public static PropertyDefinition IntRange(string name, int min, int max, int defaultValue)
        {
            List<string> values = new List<string>();

            for (int i = min; i <= max; i++)
            {
                values.Add(i.ToString());
            }

            return new PropertyDefinition(name, PropertyKind.IntRange, values, defaultValue.ToString());
        }

        public static PropertyDefinition Enum(string name, IReadOnlyList<string> values, string defaultValue)
        {
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("Enum property must have at least one value.", nameof(values));
            }

            return new PropertyDefinition(name, PropertyKind.Enum, new List<string>(values), defaultValue);
        }
    }
}
