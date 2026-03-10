using Lithforge.Runtime.Content;
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

            // Create BlockModelSO
            BlockModelSO modelSO = ScriptableObject.CreateInstance<BlockModelSO>();
            string modelPath = AssetDatabase.GenerateUniqueAssetPath(
                path + "/" + blockName + "_model.asset");
            AssetDatabase.CreateAsset(modelSO, modelPath);

            // Create BlockStateMappingSO
            BlockStateMappingSO mappingSO = ScriptableObject.CreateInstance<BlockStateMappingSO>();
            string mappingPath = AssetDatabase.GenerateUniqueAssetPath(
                path + "/" + blockName + "_blockstate.asset");
            AssetDatabase.CreateAsset(mappingSO, mappingPath);

            // Wire model into mapping's default variant
            SerializedObject mappingObj = new SerializedObject(mappingSO);
            SerializedProperty variants = mappingObj.FindProperty("_variants");
            variants.arraySize = 1;
            SerializedProperty variant0 = variants.GetArrayElementAtIndex(0);
            variant0.FindPropertyRelative("_variantKey").stringValue = "";
            variant0.FindPropertyRelative("_model").objectReferenceValue = modelSO;
            variant0.FindPropertyRelative("_weight").intValue = 1;
            mappingObj.ApplyModifiedPropertiesWithoutUndo();

            // Create BlockDefinitionSO
            BlockDefinitionSO blockSO = ScriptableObject.CreateInstance<BlockDefinitionSO>();
            string blockPath = AssetDatabase.GenerateUniqueAssetPath(
                path + "/" + blockName + ".asset");
            AssetDatabase.CreateAsset(blockSO, blockPath);

            // Wire mapping into block definition
            SerializedObject blockObj = new SerializedObject(blockSO);
            blockObj.FindProperty("_blockName").stringValue = blockName;
            blockObj.FindProperty("_blockStateMapping").objectReferenceValue = mappingSO;
            blockObj.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = blockSO;
            EditorGUIUtility.PingObject(blockSO);

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
