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
        private static readonly Regex s_validPattern = new(@"^[a-z0-9_]+:[a-z0-9_/]+$", RegexOptions.Compiled);

        /// <summary>The content namespace (e.g. "lithforge").</summary>
        public string Namespace { get; }

        /// <summary>The content name within the namespace (e.g. "stone").</summary>
        public string Name { get; }

        /// <summary>
        /// Creates a new identifier from explicit namespace and name parts.
        /// </summary>
        /// <param name="ns">Content namespace (lowercase alphanumeric + underscores).</param>
        /// <param name="name">Content name within the namespace.</param>
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

        /// <summary>
        /// Parses a "namespace:name" string into a ResourceId.
        /// </summary>
        /// <param name="raw">The colon-delimited string to parse.</param>
        /// <returns>The parsed identifier.</returns>
        /// <exception cref="FormatException">Thrown when the string does not match the required pattern.</exception>
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
