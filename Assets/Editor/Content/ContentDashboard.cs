using Lithforge.Runtime.Content;
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

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawSOSection(ref _blocksFoldout, "Blocks", _blocks);
            DrawSOSection(ref _modelsFoldout, "Block Models", _models);
            DrawSOSection(ref _itemsFoldout, "Items", _items);
            DrawSOSection(ref _biomesFoldout, "Biomes", _biomes);
            DrawSOSection(ref _oresFoldout, "Ores", _ores);
            DrawSOSection(ref _lootFoldout, "Loot Tables", _lootTables);
            DrawSOSection(ref _tagsFoldout, "Tags", _tags);
            DrawSOSection(ref _recipesFoldout, "Recipes", _recipes);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSOSection<T>(ref bool foldout, string label, T[] assets)
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
