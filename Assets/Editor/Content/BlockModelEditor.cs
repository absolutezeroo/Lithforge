using Lithforge.Runtime.Content;
using UnityEditor;
using UnityEngine;

namespace Lithforge.Editor.Content
{
    [CustomEditor(typeof(BlockModel))]
    public sealed class BlockModelEditor : UnityEditor.Editor
    {
        private SerializedProperty _parent;
        private SerializedProperty _builtInParent;
        private SerializedProperty _textures;
        private SerializedProperty _elements;
        private bool _texturesFoldout = true;
        private bool _elementsFoldout;

        private void OnEnable()
        {
            _parent = serializedObject.FindProperty("_parent");
            _builtInParent = serializedObject.FindProperty("_builtInParent");
            _textures = serializedObject.FindProperty("_textures");
            _elements = serializedObject.FindProperty("_elements");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BlockModel model = (BlockModel)target;

            EditorGUILayout.LabelField("Block Model", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(_parent, new GUIContent("Parent Model"));
            EditorGUILayout.PropertyField(_builtInParent, new GUIContent("Built-In Parent Type"));

            // Show warning if both parent types are set
            if (model.Parent != null && model.BuiltInParent != BuiltInParentType.None)
            {
                EditorGUILayout.HelpBox(
                    "Both Parent and Built-In Parent are set. " +
                    "Built-In Parent will take priority when Parent chain is resolved.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            _texturesFoldout = EditorGUILayout.Foldout(_texturesFoldout, "Texture Variables", true);

            if (_texturesFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_textures, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            _elementsFoldout = EditorGUILayout.Foldout(_elementsFoldout, "Model Elements", true);

            if (_elementsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_elements, true);
                EditorGUI.indentLevel--;
            }

            // Parent chain info
            EditorGUILayout.Space(8);
            string parentChain = BuildParentChainDescription(model);
            EditorGUILayout.HelpBox("Parent Chain: " + parentChain, MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private static string BuildParentChainDescription(BlockModel model)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(model.name);

            BlockModel current = model.Parent;
            int depth = 0;

            while (current != null && depth < 10)
            {
                sb.Append(" -> ");
                sb.Append(current.name);
                current = current.Parent;
                depth++;
            }

            if (model.BuiltInParent != BuiltInParentType.None)
            {
                sb.Append(" -> [");
                sb.Append(model.BuiltInParent.ToString());
                sb.Append("]");
            }
            else if (current == null && depth > 0)
            {
                sb.Append(" -> [terminal]");
            }

            return sb.ToString();
        }
    }
}
