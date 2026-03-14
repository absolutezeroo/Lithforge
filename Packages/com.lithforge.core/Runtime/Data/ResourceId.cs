using System;
using System.Text.RegularExpressions;

namespace Lithforge.Core.Data
{
    /// <summary>
    /// Immutable "namespace:path" identifier for all registered content.
    /// Format: lowercase alphanumeric with underscores and forward slashes.
    /// Example: "lithforge:stone", "lithforge:oak_log"
    /// </summary>
    public readonly struct ResourceId : IEquatable<ResourceId>
    {
        private static readonly Regex s_validPattern =
            new Regex(@"^[a-z0-9_]+:[a-z0-9_/]+$", RegexOptions.Compiled);

        public string Namespace { get; }

        public string Name { get; }

        public ResourceId(string ns, string name)
        {
            if (string.IsNullOrEmpty(ns))
            {
                throw new ArgumentException("Namespace cannot be null or empty.", nameof(ns));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));
            }

            Namespace = ns;
            Name = name;
        }

        public static ResourceId Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                throw new ArgumentException("ResourceId string cannot be null or empty.", nameof(raw));
            }

            if (!s_validPattern.IsMatch(raw))
            {
                throw new FormatException(
                    $"ResourceId '{raw}' does not match required format '^[a-z0-9_]+:[a-z0-9_/]+$'.");
            }

            int colon = raw.IndexOf(':');

            return new ResourceId(raw.Substring(0, colon), raw.Substring(colon + 1));
        }

        public static bool TryParse(string raw, out ResourceId result)
        {
            result = default;

            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            if (!s_validPattern.IsMatch(raw))
            {
                return false;
            }

            int colon = raw.IndexOf(':');
            result = new ResourceId(raw.Substring(0, colon), raw.Substring(colon + 1));

            return true;
        }

        public override string ToString()
        {
            return $"{Namespace}:{Name}";
        }

        public bool Equals(ResourceId other)
        {
            return string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
                && string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Namespace, Name);
        }

        public static bool operator ==(ResourceId left, ResourceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ResourceId left, ResourceId right)
        {
            return !left.Equals(right);
        }
    }
}
