using System;
using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content.Tools
{
    [CreateAssetMenu(fileName = "NewToolDefinition",
        menuName = "Lithforge/Content/Tool Definition")]
    public sealed class ToolDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The ToolType enum this definition configures")]
        public ToolType toolType;

        [Header("Sprite Compositing")]
        [Tooltip("Resource path relative to Content/Textures/Items/Tool/ (e.g. 'Pickaxe')")]
        public string textureFolderName;

        [Tooltip("Layers composited bottom-to-top. First entry = bottom layer, last = top layer.")]
        public SpriteLayer[] spriteLayers;

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
        [Tooltip("Subfolder name under the tool's texture folder (e.g. 'Handle', 'Head', 'Binding')")]
        public string textureSubfolder;

        [Tooltip("Which ToolPartTypes map to this layer. First match wins when resolving parts.")]
        public ToolPartType[] partTypes;

        [Tooltip("Prefix used in texture filenames (e.g. 'head', 'handle', 'binding', 'blade')")]
        public string filenamePrefix;
    }
}
