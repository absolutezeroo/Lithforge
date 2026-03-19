using System;
using Lithforge.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content.Tools
{
    /// <summary>
    ///     Data-driven tool type definition (e.g. pickaxe, axe, shovel) that configures
    ///     sprite compositing layers and required part types for modular tool assembly.
    /// </summary>
    [CreateAssetMenu(fileName = "NewToolDefinition",
        menuName = "Lithforge/Content/Tool Definition")]
    public sealed class ToolDefinition : ScriptableObject
    {
        /// <summary>The tool category this definition configures (e.g. Pickaxe, Axe, Shovel).</summary>
        [Header("Identity")]
        [Tooltip("The ToolType enum this definition configures")]
        public ToolType toolType;

        /// <summary>Resource path relative to Content/Textures/Items/Tool/ for sprite compositing.</summary>
        [Header("Sprite Compositing")]
        [Tooltip("Resource path relative to Content/Textures/Items/Tool/ (e.g. 'Pickaxe')")]
        public string textureFolderName;

        /// <summary>Compositing layers ordered bottom-to-top for tool sprite generation.</summary>
        [Tooltip("Layers composited bottom-to-top. First entry = bottom layer, last = top layer.")]
        public SpriteLayer[] spriteLayers;

        /// <summary>Part types required for assembly, validated by ToolAssembler.</summary>
        [Header("Required Parts")]
        [Tooltip("Part types required for assembly (validated by ToolAssembler)")]
        public ToolPartType[] requiredParts;
    }

    /// <summary>
    /// One compositing layer: maps a set of ToolPartTypes to a texture subfolder.
    /// </summary>
    [Serializable]
    public struct SpriteLayer
    {
        /// <summary>Subfolder name under the tool's texture folder (e.g. "Handle", "Head", "Binding").</summary>
        [Tooltip("Subfolder name under the tool's texture folder (e.g. 'Handle', 'Head', 'Binding')")]
        public string textureSubfolder;

        /// <summary>ToolPartTypes that map to this layer; first match wins when resolving parts.</summary>
        [Tooltip("Which ToolPartTypes map to this layer. First match wins when resolving parts.")]
        public ToolPartType[] partTypes;

        /// <summary>Prefix used in texture filenames (e.g. "head", "handle", "binding", "blade").</summary>
        [Tooltip("Prefix used in texture filenames (e.g. 'head', 'handle', 'binding', 'blade')")]
        public string filenamePrefix;
    }
}
