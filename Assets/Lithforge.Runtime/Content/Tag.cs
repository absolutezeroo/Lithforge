using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewTag", menuName = "Lithforge/Content/Tag", order = 5)]
    public sealed class Tag : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Tag name (e.g. 'blocks/mineable_pickaxe')")]
        [SerializeField] private string tagName = "";

        [Header("Behavior")]
        [Tooltip("If true, this tag replaces any existing entries instead of appending")]
        [SerializeField] private bool replace;

        [Header("Entries")]
        [Tooltip("ScriptableObject entries (blocks, items, etc.) that belong to this tag")]
        [SerializeField] private List<ScriptableObject> entries = new List<ScriptableObject>();

        [Tooltip("String entry ids (for backward compatibility or cross-reference)")]
        [SerializeField] private List<string> entryIds = new List<string>();

        public string Namespace
        {
            get { return _namespace; }
        }

        public string TagName
        {
            get { return tagName; }
        }

        public bool Replace
        {
            get { return replace; }
        }

        public IReadOnlyList<ScriptableObject> Entries
        {
            get { return entries; }
        }

        public IReadOnlyList<string> EntryIds
        {
            get { return entryIds; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(tagName))
            {
                tagName = name;
            }
        }
    }
}
