using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewTag", menuName = "Lithforge/Content/Tag", order = 5)]
    public sealed class TagSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Namespace for the resource id")]
        [SerializeField] private string _namespace = "lithforge";

        [Tooltip("Tag name (e.g. 'blocks/mineable_pickaxe')")]
        [SerializeField] private string _tagName = "";

        [Header("Behavior")]
        [Tooltip("If true, this tag replaces any existing entries instead of appending")]
        [SerializeField] private bool _replace;

        [Header("Entries")]
        [Tooltip("ScriptableObject entries (blocks, items, etc.) that belong to this tag")]
        [SerializeField] private List<ScriptableObject> _entries = new List<ScriptableObject>();

        [Tooltip("String entry ids (for backward compatibility or cross-reference)")]
        [SerializeField] private List<string> _entryIds = new List<string>();

        public string Namespace
        {
            get { return _namespace; }
        }

        public string TagName
        {
            get { return _tagName; }
        }

        public bool Replace
        {
            get { return _replace; }
        }

        public IReadOnlyList<ScriptableObject> Entries
        {
            get { return _entries; }
        }

        public IReadOnlyList<string> EntryIds
        {
            get { return _entryIds; }
        }
    }
}
