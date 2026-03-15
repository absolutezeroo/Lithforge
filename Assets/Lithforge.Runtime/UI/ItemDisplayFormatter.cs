namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Converts raw item identifiers into short, capitalized display labels
    /// suitable for hotbar slots and compact UI elements.
    /// </summary>
    internal static class ItemDisplayFormatter
    {
        /// <summary>
        /// Produces a short display label from a snake_case item name.
        /// Takes the segment after the last underscore (or the whole name if none),
        /// truncates to 6 characters, and capitalizes the first letter.
        /// </summary>
        /// <param name="name">
        /// Raw item name in snake_case (e.g. "oak_planks"). May be null or empty.
        /// </param>
        /// <returns>
        /// A human-readable label no longer than 6 characters (e.g. "Planks"),
        /// or an empty string if <paramref name="name"/> is null or empty.
        /// </returns>
        public static string FormatItemName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            // Convert snake_case to short display name
            // e.g. "cobblestone" -> "Cobble", "oak_planks" -> "Planks"
            int underscoreIndex = name.LastIndexOf('_');

            if (underscoreIndex >= 0 && underscoreIndex < name.Length - 1)
            {
                name = name.Substring(underscoreIndex + 1);
            }

            if (name.Length > 6)
            {
                name = name.Substring(0, 6);
            }

            if (name.Length > 0)
            {
                return char.ToUpper(name[0]) + name.Substring(1);
            }

            return name;
        }
    }
}
