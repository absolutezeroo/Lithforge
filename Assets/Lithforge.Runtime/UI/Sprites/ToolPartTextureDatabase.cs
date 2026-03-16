using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Tools;
using Lithforge.Voxel.Item;

using UnityEngine;

namespace Lithforge.Runtime.UI.Sprites
{
    /// <summary>
    ///     Indexes tool part layer textures for sprite compositing.
    ///     Driven entirely by ToolDefinition and ToolMaterialDefinition assets.
    /// </summary>
    public sealed class ToolPartTextureDatabase
    {
        private const string BasePath = "Content/Textures/Items/Tool";
        private readonly string _fallbackSuffix;
        private readonly Dictionary<(ToolType, string, string), Texture2D> _layers;
        private readonly Dictionary<string, string> _materialSuffixes;
        private readonly Dictionary<ToolType, ToolDefinition> _toolDefs;

        public ToolPartTextureDatabase(
            ToolDefinition[] toolDefinitions,
            ToolMaterialDefinition[] materials)
        {
            _layers = new Dictionary<(ToolType, string, string), Texture2D>();
            _toolDefs = new Dictionary<ToolType, ToolDefinition>();
            _materialSuffixes = new Dictionary<string, string>(materials.Length);

            // Resolve fallback material suffix and pre-cache all material suffixes
            _fallbackSuffix = null;

            for (int i = 0; i < materials.Length; i++)
            {
                ToolMaterialDefinition mat = materials[i];

                if (mat.isFallbackMaterial)
                {
                    _fallbackSuffix = !string.IsNullOrEmpty(mat.textureSuffix)
                        ? mat.textureSuffix
                        : mat.materialId;
                }

                if (!string.IsNullOrEmpty(mat.materialId))
                {
                    string suffix = !string.IsNullOrEmpty(mat.textureSuffix)
                        ? mat.textureSuffix
                        : mat.materialId;

                    // materialId on SO is "lithforge:iron"; extract Name part for default suffix
                    if (string.IsNullOrEmpty(mat.textureSuffix) &&
                        ResourceId.TryParse(mat.materialId, out ResourceId parsedId))
                    {
                        suffix = parsedId.Name;
                    }

                    _materialSuffixes[mat.materialId] = suffix;
                }
            }

            // Index tool definitions
            for (int i = 0; i < toolDefinitions.Length; i++)
            {
                ToolDefinition def = toolDefinitions[i];
                _toolDefs[def.toolType] = def;

                if (def.spriteLayers == null)
                {
                    continue;
                }

                // Load textures for each layer defined in the SO
                for (int l = 0; l < def.spriteLayers.Length; l++)
                {
                    SpriteLayer layer = def.spriteLayers[l];
                    string path = $"{BasePath}/{def.textureFolderName}/{layer.textureSubfolder}";
                    Texture2D[] textures = Resources.LoadAll<Texture2D>(path);

                    for (int t = 0; t < textures.Length; t++)
                    {
                        Texture2D tex = textures[t];

                        // Skip broken variants (for inventory display)
                        if (tex.name.Contains("broken"))
                        {
                            continue;
                        }

                        // Extract material suffix from filename using the layer's prefix
                        string extractedSuffix = ExtractSuffix(tex.name, layer.filenamePrefix);

                        if (extractedSuffix != null)
                        {
                            _layers[(def.toolType, layer.textureSubfolder, extractedSuffix)] = tex;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Gets the ToolDefinition for a given tool type. Returns null if not configured.
        /// </summary>
        public ToolDefinition GetDefinition(ToolType toolType)
        {
            _toolDefs.TryGetValue(toolType, out ToolDefinition def);
            return def;
        }

        /// <summary>
        ///     Gets a texture layer for the given tool type, subfolder, and material suffix.
        ///     Falls back to the configured fallback material if exact match not found.
        ///     Returns null if no texture exists at all.
        /// </summary>
        public Texture2D GetLayer(ToolType toolType, string subfolder, string materialSuffix)
        {
            if (_layers.TryGetValue((toolType, subfolder, materialSuffix), out Texture2D tex))
            {
                return tex;
            }

            if (_fallbackSuffix != null &&
                _layers.TryGetValue((toolType, subfolder, _fallbackSuffix), out Texture2D fb))
            {
                return fb;
            }

            return null;
        }

        /// <summary>
        ///     Resolves the texture suffix for a material ResourceId.
        ///     Uses the pre-cached suffix from ToolMaterialDefinition if available,
        ///     otherwise falls back to materialId.Name.
        /// </summary>
        public string ResolveSuffix(ResourceId materialId)
        {
            if (_materialSuffixes.TryGetValue(materialId.ToString(), out string suffix))
            {
                return suffix;
            }

            return materialId.Name;
        }

        /// <summary>
        ///     Searches all registered ToolDefinitions for a texture matching
        ///     the given part type and material suffix. Returns the first match.
        /// </summary>
        public Texture2D FindPartTexture(ToolPartType partType, string materialSuffix)
        {
            foreach (KeyValuePair<ToolType, ToolDefinition> kvp in _toolDefs)
            {
                ToolDefinition def = kvp.Value;

                if (def.spriteLayers == null)
                {
                    continue;
                }

                for (int l = 0; l < def.spriteLayers.Length; l++)
                {
                    SpriteLayer layer = def.spriteLayers[l];

                    if (layer.partTypes == null)
                    {
                        continue;
                    }

                    for (int pt = 0; pt < layer.partTypes.Length; pt++)
                    {
                        if (layer.partTypes[pt] == partType)
                        {
                            Texture2D tex = GetLayer(kvp.Key, layer.textureSubfolder, materialSuffix);

                            if (tex != null)
                            {
                                return tex;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     Extracts the material suffix from a texture filename given the expected prefix.
        ///     "head_wood" with prefix "head" returns "wood".
        ///     "blade_iron" with prefix "blade" returns "iron".
        ///     "handle_amethyst_bronze" with prefix "handle" returns "amethyst_bronze".
        /// </summary>
        private static string ExtractSuffix(string textureName, string prefix)
        {
            string expected = prefix + "_";

            if (textureName.StartsWith(expected) && textureName.Length > expected.Length)
            {
                return textureName.Substring(expected.Length);
            }

            return null;
        }
    }
}
