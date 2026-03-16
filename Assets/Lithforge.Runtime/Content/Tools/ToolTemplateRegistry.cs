using System;
using System.Collections.Generic;
using Lithforge.Core.Data;

namespace Lithforge.Runtime.Content.Tools
{
    /// <summary>
    /// Provides pre-baked ToolInstance CustomData for legacy tool items.
    /// Populated by ContentPipeline at startup. Each call to GetTemplate
    /// returns a fresh copy so each ItemStack gets independent durability.
    /// </summary>
    public static class ToolTemplateRegistry
    {
        private static Dictionary<ResourceId, byte[]> s_templates
            = new Dictionary<ResourceId, byte[]>();

        public static void Initialize(Dictionary<ResourceId, byte[]> templates)
        {
            s_templates = templates ?? new Dictionary<ResourceId, byte[]>();
        }

        /// <summary>
        /// Returns a COPY of the template CustomData for this item, or null if not a legacy tool.
        /// </summary>
        public static byte[] GetTemplate(ResourceId itemId)
        {
            if (s_templates.TryGetValue(itemId, out byte[] template))
            {
                byte[] copy = new byte[template.Length];
                Array.Copy(template, copy, template.Length);
                return copy;
            }

            return null;
        }

        public static bool IsLegacyTool(ResourceId itemId)
        {
            return s_templates.ContainsKey(itemId);
        }

        public static void Clear()
        {
            s_templates.Clear();
        }
    }
}