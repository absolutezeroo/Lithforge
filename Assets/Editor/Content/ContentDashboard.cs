using System.Collections.Generic;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Items;
using Lithforge.Runtime.Content.Loot;
using Lithforge.Runtime.Content.Models;
using Lithforge.Runtime.Content.Recipes;
using Lithforge.Runtime.Content.Tags;
using Lithforge.Runtime.Content.WorldGen;
using UnityEditor;
using UnityEngine;

namespace Lithforge.Editor.Content
{
    public sealed class ContentDashboard : EditorWindow
    {
        private Vector2 _scrollPos;

        private BlockDefinition[] _blocks;
        private BlockStateMapping[] _blockStates;
        private BlockModel[] _models;
        private ItemDefinition[] _items;
        private BiomeDefinition[] _biomes;
        private OreDefinition[] _ores;
        private LootTable[] _lootTables;
        private Tag[] _tags;
        private RecipeDefinition[] _recipes;

        private bool _blocksFoldout = true;
        private bool _modelsFoldout;
        private bool _itemsFoldout;
        private bool _biomesFoldout;
        private bool _oresFoldout;
        private bool _lootFoldout;
        private bool _tagsFoldout;
        private bool _recipesFoldout;
        private bool _validationFoldout = true;

        private List<string> _validationWarnings = new List<string>();

        [MenuItem("Lithforge/Content Dashboard")]
        public static void ShowWindow()
        {
            ContentDashboard window = GetWindow<ContentDashboard>("Content Dashboard");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void Refresh()
        {
            _blocks = LoadAll<BlockDefinition>();
            _blockStates = LoadAll<BlockStateMapping>();
            _models = LoadAll<BlockModel>();
            _items = LoadAll<ItemDefinition>();
            _biomes = LoadAll<BiomeDefinition>();
            _ores = LoadAll<OreDefinition>();
            _lootTables = LoadAll<LootTable>();
            _tags = LoadAll<Tag>();
            _recipes = LoadAll<RecipeDefinition>();
            RunValidation();
        }

        private void RunValidation()
        {
            _validationWarnings.Clear();

            // Check blocks without blockstate mappings
            for (int i = 0; i < _blocks.Length; i++)
            {
                BlockDefinition block = _blocks[i];

                if (block == null)
                {
                    continue;
                }

                if (block.BlockStateMapping == null)
                {
                    _validationWarnings.Add($"Block '{block.name}' has no BlockStateMapping assigned.");
                }

                if (block.LootTable == null && !block.BlockName.Equals("air"))
                {
                    _validationWarnings.Add($"Block '{block.name}' has no LootTable assigned.");
                }
            }

            // Check blockstate mappings with null model references
            for (int i = 0; i < _blockStates.Length; i++)
            {
                BlockStateMapping mapping = _blockStates[i];

                if (mapping == null)
                {
                    continue;
                }

                IReadOnlyList<BlockStateVariantEntry> variants = mapping.Variants;

                for (int v = 0; v < variants.Count; v++)
                {
                    if (variants[v].Model == null)
                    {
                        _validationWarnings.Add(
                            $"BlockStateMapping '{mapping.name}' variant '{variants[v].VariantKey}' has no model assigned.");
                    }
                }
            }

            // Check models with broken parent chains
            for (int i = 0; i < _models.Length; i++)
            {
                BlockModel model = _models[i];

                if (model == null)
                {
                    continue;
                }

                if (model.Parent == null && model.BuiltInParent == BuiltInParentType.None &&
                    model.Textures.Count == 0 && model.Elements.Count == 0)
                {
                    _validationWarnings.Add($"Model '{model.name}' has no parent, no textures, and no elements.");
                }
            }
        }

        private static T[] LoadAll<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            T[] results = new T[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                results[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return results;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Lithforge Content Dashboard", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                Refresh();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Summary bar
            EditorGUILayout.HelpBox(
                $"Blocks: {_blocks.Length}  |  Models: {_models.Length}  |  " +
                $"Items: {_items.Length}  |  Biomes: {_biomes.Length}  |  " +
                $"Ores: {_ores.Length}  |  Loot: {_lootTables.Length}  |  " +
                $"Tags: {_tags.Length}  |  Recipes: {_recipes.Length}",
                MessageType.None);

            EditorGUILayout.Space(8);

            // Validation warnings
            if (_validationWarnings.Count > 0)
            {
                _validationFoldout = EditorGUILayout.Foldout(
                    _validationFoldout, $"Warnings ({_validationWarnings.Count})", true);

                if (_validationFoldout)
                {
                    for (int i = 0; i < _validationWarnings.Count; i++)
                    {
                        EditorGUILayout.HelpBox(_validationWarnings[i], MessageType.Warning);
                    }
                }

                EditorGUILayout.Space(8);
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawSoSection(ref _blocksFoldout, "Blocks", _blocks);
            DrawSoSection(ref _modelsFoldout, "Block Models", _models);
            DrawSoSection(ref _itemsFoldout, "Items", _items);
            DrawSoSection(ref _biomesFoldout, "Biomes", _biomes);
            DrawSoSection(ref _oresFoldout, "Ores", _ores);
            DrawSoSection(ref _lootFoldout, "Loot Tables", _lootTables);
            DrawSoSection(ref _tagsFoldout, "Tags", _tags);
            DrawSoSection(ref _recipesFoldout, "Recipes", _recipes);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSoSection<T>(ref bool foldout, string label, T[] assets)
            where T : ScriptableObject
        {
            foldout = EditorGUILayout.Foldout(foldout, $"{label} ({assets.Length})", true);

            if (!foldout)
            {
                return;
            }

            EditorGUI.indentLevel++;

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] == null)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(assets[i].name, GUILayout.MinWidth(200));

                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = assets[i];
                    EditorGUIUtility.PingObject(assets[i]);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }
    }
}
