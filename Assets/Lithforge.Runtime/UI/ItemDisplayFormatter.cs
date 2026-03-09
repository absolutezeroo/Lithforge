namespace Lithforge.Runtime.UI
{
    internal static class ItemDisplayFormatter
    {
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
