using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Lithforge.Runtime.Content.Models;
using UnityEditor;
using UnityEngine;

namespace Lithforge.Editor.Content
{
    /// <summary>
    /// One-time migration script that converts BlockModel texture variables from
    /// string-based resource paths (e.g. "lithforge:block/stone") to direct Texture2D
    /// references. Reads old serialized _value strings from raw YAML since the field
    /// was removed from the TextureVariable class.
    /// </summary>
    public static class MigrateTexturesToDirect
    {
        private const string _blockTextureFolder = "Assets/Resources/Content/Textures/Blocks";
        private const string _itemTextureFolder = "Assets/Resources/Content/Textures/Items";

        [MenuItem("Lithforge/Migrate Textures to Direct References")]
        private static void Migrate()
        {
            string[] guids = AssetDatabase.FindAssets("t:BlockModel");
            int migratedCount = 0;
            int skippedCount = 0;

            for (int g = 0; g < guids.Length; g++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[g]);

                // Read raw YAML to extract old _value strings
                string yaml = File.ReadAllText(assetPath);
                List<TextureEntry> entries = ParseTextureEntries(yaml);

                if (entries.Count == 0)
                {
                    skippedCount++;
                    continue;
                }

                BlockModel model = AssetDatabase.LoadAssetAtPath<BlockModel>(assetPath);

                if (model == null)
                {
                    Debug.LogWarning($"[MigrateTextures] Could not load BlockModel at '{assetPath}'.");
                    skippedCount++;
                    continue;
                }

                SerializedObject so = new SerializedObject(model);
                SerializedProperty texturesProp = so.FindProperty("textures");

                if (texturesProp == null)
                {
                    Debug.LogWarning($"[MigrateTextures] Could not find 'textures' property on '{assetPath}'.");
                    skippedCount++;
                    continue;
                }

                texturesProp.arraySize = entries.Count;

                for (int i = 0; i < entries.Count; i++)
                {
                    SerializedProperty elem = texturesProp.GetArrayElementAtIndex(i);
                    SerializedProperty variableProp = elem.FindPropertyRelative("variable");
                    SerializedProperty textureProp = elem.FindPropertyRelative("texture");
                    SerializedProperty variableReferenceProp = elem.FindPropertyRelative("variableReference");

                    variableProp.stringValue = entries[i].Variable;

                    if (entries[i].Value.StartsWith("#"))
                    {
                        variableReferenceProp.stringValue = entries[i].Value;
                        textureProp.objectReferenceValue = null;
                    }
                    else
                    {
                        string textureName = ExtractTextureName(entries[i].Value);
                        Texture2D tex = FindTexture(textureName);

                        if (tex == null)
                        {
                            Debug.LogWarning(
                                $"[MigrateTextures] Texture not found for '{entries[i].Value}' " +
                                $"(looked for '{textureName}.png') in '{model.name}'.");
                        }

                        textureProp.objectReferenceValue = tex;
                        variableReferenceProp.stringValue = "";
                    }
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(model);
                migratedCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[MigrateTextures] Migration complete: {migratedCount} migrated, {skippedCount} skipped.");
        }

        private static List<TextureEntry> ParseTextureEntries(string yaml)
        {
            List<TextureEntry> results = new List<TextureEntry>();

            // Match pairs of _variable/_value lines within the _textures list.
            // Handles both _variable/_value (old serialization) and variable/value (post-rename).
            MatchCollection matches = Regex.Matches(
                yaml,
                @"[_]?variable:\s*(.+)\s*\n\s*[_]?value:\s*(.+)",
                RegexOptions.Multiline);

            for (int i = 0; i < matches.Count; i++)
            {
                string variable = matches[i].Groups[1].Value.Trim();
                string value = matches[i].Groups[2].Value.Trim();

                // Strip YAML quotes if present
                variable = StripYamlQuotes(variable);
                value = StripYamlQuotes(value);

                if (!string.IsNullOrEmpty(variable))
                {
                    results.Add(new TextureEntry(variable, value));
                }
            }

            return results;
        }

        private static string StripYamlQuotes(string input)
        {
            if (input.Length >= 2)
            {
                if ((input[0] == '\'' && input[input.Length - 1] == '\'') ||
                    (input[0] == '"' && input[input.Length - 1] == '"'))
                {
                    return input.Substring(1, input.Length - 2);
                }
            }

            return input;
        }

        /// <summary>
        /// Extracts texture name from a resource path string.
        /// "lithforge:block/stone" → "stone"
        /// "lithforge:block/oak_log_top" → "oak_log_top"
        /// </summary>
        private static string ExtractTextureName(string resourcePath)
        {
            // Strip namespace prefix (everything before and including ':')
            int colonIndex = resourcePath.IndexOf(':');

            if (colonIndex >= 0)
            {
                resourcePath = resourcePath.Substring(colonIndex + 1);
            }

            // Strip path prefix (everything before and including last '/')
            int slashIndex = resourcePath.LastIndexOf('/');

            if (slashIndex >= 0)
            {
                resourcePath = resourcePath.Substring(slashIndex + 1);
            }

            return resourcePath;
        }

        private static Texture2D FindTexture(string textureName)
        {
            // Try block textures first
            string blockPath = _blockTextureFolder + "/" + textureName + ".png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(blockPath);

            if (tex != null)
            {
                return tex;
            }

            // Try item textures
            string itemPath = _itemTextureFolder + "/" + textureName + ".png";
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(itemPath);

            if (tex != null)
            {
                return tex;
            }

            // Fallback: search by name across all textures
            string[] searchGuids = AssetDatabase.FindAssets(textureName + " t:Texture2D");

            for (int i = 0; i < searchGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(searchGuids[i]);

                if (path.EndsWith(textureName + ".png"))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            return null;
        }

        private readonly struct TextureEntry
        {
            public readonly string Variable;
            public readonly string Value;

            public TextureEntry(string variable, string value)
            {
                Variable = variable;
                Value = value;
            }
        }
    }
}
