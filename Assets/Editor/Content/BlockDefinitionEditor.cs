using Lithforge.Runtime.Content;
using UnityEditor;
using UnityEngine;

namespace Lithforge.Editor.Content
{
    [CustomEditor(typeof(BlockDefinition))]
    public sealed class BlockDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _namespace;
        private SerializedProperty _blockName;
        private SerializedProperty _hardness;
        private SerializedProperty _blastResistance;
        private SerializedProperty _requiresTool;
        private SerializedProperty _soundGroup;
        private SerializedProperty _collisionShape;
        private SerializedProperty _renderLayer;
        private SerializedProperty _lightEmission;
        private SerializedProperty _lightFilter;
        private SerializedProperty _mapColor;
        private SerializedProperty _lootTable;
        private SerializedProperty _blockStateMapping;
        private SerializedProperty _properties;
        private SerializedProperty _tags;

        private bool _propertiesFoldout = true;
        private bool _tagsFoldout = true;

        private void OnEnable()
        {
            _namespace = serializedObject.FindProperty("_namespace");
            _blockName = serializedObject.FindProperty("_blockName");
            _hardness = serializedObject.FindProperty("_hardness");
            _blastResistance = serializedObject.FindProperty("_blastResistance");
            _requiresTool = serializedObject.FindProperty("_requiresTool");
            _soundGroup = serializedObject.FindProperty("_soundGroup");
            _collisionShape = serializedObject.FindProperty("_collisionShape");
            _renderLayer = serializedObject.FindProperty("_renderLayer");
            _lightEmission = serializedObject.FindProperty("_lightEmission");
            _lightFilter = serializedObject.FindProperty("_lightFilter");
            _mapColor = serializedObject.FindProperty("_mapColor");
            _lootTable = serializedObject.FindProperty("_lootTable");
            _blockStateMapping = serializedObject.FindProperty("_blockStateMapping");
            _properties = serializedObject.FindProperty("_properties");
            _tags = serializedObject.FindProperty("_tags");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BlockDefinition blockDef = (BlockDefinition)target;

            // Identity header with computed ResourceId
            EditorGUILayout.LabelField("Resource ID", blockDef.Namespace + ":" + blockDef.BlockName, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(_namespace);
            EditorGUILayout.PropertyField(_blockName);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Gameplay", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_hardness);
            EditorGUILayout.PropertyField(_blastResistance);
            EditorGUILayout.PropertyField(_requiresTool);
            EditorGUILayout.PropertyField(_soundGroup);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Physics", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_collisionShape);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_renderLayer);
            EditorGUILayout.PropertyField(_lightEmission);
            EditorGUILayout.PropertyField(_lightFilter);
            EditorGUILayout.PropertyField(_mapColor);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_lootTable);
            EditorGUILayout.PropertyField(_blockStateMapping);

            EditorGUILayout.Space(8);
            _propertiesFoldout = EditorGUILayout.Foldout(_propertiesFoldout, "Properties", true);

            if (_propertiesFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_properties, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            _tagsFoldout = EditorGUILayout.Foldout(_tagsFoldout, "Tags", true);

            if (_tagsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_tags, true);
                EditorGUI.indentLevel--;
            }

            // Summary box
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                $"State Count: {blockDef.ComputeStateCount()}\n" +
                $"Render Layer: {blockDef.RenderLayerString}\n" +
                $"Collision: {blockDef.CollisionShapeString}",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
