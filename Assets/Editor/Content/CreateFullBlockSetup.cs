using Lithforge.Runtime.Content.Blocks;
using Lithforge.Runtime.Content.Models;
using UnityEditor;
using UnityEngine;

namespace Lithforge.Editor.Content
{
    public static class CreateFullBlockSetup
    {
        [MenuItem("Assets/Create/Lithforge/Full Block Setup", false, 0)]
        private static void CreateBlockSetup()
        {
            string path = GetSelectedFolderPath();

            // Prompt for block name
            string blockName = "new_block";

            // Create BlockModel
            BlockModel model = ScriptableObject.CreateInstance<BlockModel>();
            string modelPath = AssetDatabase.GenerateUniqueAssetPath(
                path + "/" + blockName + "_model.asset");
            AssetDatabase.CreateAsset(model, modelPath);

            // Create BlockStateMapping
            BlockStateMapping mapping = ScriptableObject.CreateInstance<BlockStateMapping>();
            string mappingPath = AssetDatabase.GenerateUniqueAssetPath(
                path + "/" + blockName + "_blockstate.asset");
            AssetDatabase.CreateAsset(mapping, mappingPath);

            // Wire model into mapping's default variant
            SerializedObject mappingObj = new SerializedObject(mapping);
            SerializedProperty variants = mappingObj.FindProperty("_variants");
            variants.arraySize = 1;
            SerializedProperty variant0 = variants.GetArrayElementAtIndex(0);
            variant0.FindPropertyRelative("_variantKey").stringValue = "";
            variant0.FindPropertyRelative("_model").objectReferenceValue = model;
            variant0.FindPropertyRelative("_weight").intValue = 1;
            mappingObj.ApplyModifiedPropertiesWithoutUndo();

            // Create BlockDefinition
            BlockDefinition block = ScriptableObject.CreateInstance<BlockDefinition>();
            string blockPath = AssetDatabase.GenerateUniqueAssetPath(
                path + "/" + blockName + ".asset");
            AssetDatabase.CreateAsset(block, blockPath);

            // Wire mapping into block definition
            SerializedObject blockObj = new SerializedObject(block);
            blockObj.FindProperty("_blockName").stringValue = blockName;
            blockObj.FindProperty("_blockStateMapping").objectReferenceValue = mapping;
            blockObj.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = block;
            EditorGUIUtility.PingObject(block);

            Debug.Log(
                $"[Lithforge] Created full block setup: {blockPath}, {mappingPath}, {modelPath}");
        }

        private static string GetSelectedFolderPath()
        {
            string path = "Assets";

            foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);

                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (System.IO.Directory.Exists(assetPath))
                    {
                        path = assetPath;
                    }
                    else
                    {
                        path = System.IO.Path.GetDirectoryName(assetPath);
                    }

                    break;
                }
            }

            return path;
        }
    }
}
