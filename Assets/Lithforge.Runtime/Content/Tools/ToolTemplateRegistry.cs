using System;
using System.Collections.Generic;

using Lithforge.Core.Data;

namespace Lithforge.Runtime.Content.Tools
{
    /// <summary>
    /// Provides pre-baked ToolInstance CustomData for legacy tool items.
    /// Populated by ContentPipeline at startup, stored in ContentPipelineResult.
    /// Each call to GetTemplate returns a fresh copy so each ItemStack gets
    /// independent durability.
    /// </summary>
    public sealed class ToolTemplateRegistry
    {
        private readonly Dictionary<ResourceId, byte[]> _templates;

        public ToolTemplateRegistry(Dictionary<ResourceId, byte[]> templates)
        {
            _templates = new Dictionary<ResourceId, byte[]>();

            if (templates != null)
            {
                foreach (KeyValuePair<ResourceId, byte[]> pair in templates)
                {
                    _templates[pair.Key] = pair.Value;
                }
            }
        }

        /// <summary>
        /// Returns a COPY of the template CustomData for this item, or null if not a legacy tool.
        /// </summary>
        public byte[] GetTemplate(ResourceId itemId)
        {
            if (_templates.TryGetValue(itemId, out byte[] template))
            {
                byte[] copy = new byte[template.Length];

                Array.Copy(template, copy, template.Length);

                return copy;
            }

            return null;
        }

        public bool IsLegacyTool(ResourceId itemId)
        {
            return _templates.ContainsKey(itemId);
        }
    }
}
