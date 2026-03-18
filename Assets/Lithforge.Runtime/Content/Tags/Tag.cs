using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Tags
{
    /// <summary>
    ///     Groups blocks or items under a shared label (e.g. "blocks/mineable_pickaxe") so recipes,
    ///     loot tables, and tool logic can reference entire categories instead of individual ids.
    ///     Loaded by <c>TagLoader</c> into <c>TagRegistry</c> during ContentPipeline Phase 10.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTag", menuName = "Lithforge/Content/Tag", order = 5)]
    public sealed class Tag : ScriptableObject
    {
        /// <summary>Resource-id namespace (typically "lithforge").</summary>
        [FormerlySerializedAs("_namespace"), Header("Identity"), Tooltip("Namespace for the resource id"), SerializeField]
         private string @namespace = "lithforge";

        /// <summary>
        ///     Slash-delimited name identifying the tag, e.g. "blocks/mineable_pickaxe".
        ///     Combined with <see cref="Namespace" /> to form the full ResourceId.
        /// </summary>
        [FormerlySerializedAs("_tagName"), Tooltip("Tag name (e.g. 'blocks/mineable_pickaxe')"), SerializeField]
         private string tagName = "";

        /// <summary>When true, this tag clears any previously registered entries before adding its own.</summary>
        [FormerlySerializedAs("_replace"), Header("Behavior"), Tooltip("If true, this tag replaces any existing entries instead of appending"), SerializeField]
         private bool replace;

        /// <summary>Direct SO references (BlockDefinition, ItemDefinition, etc.) that belong to this tag.</summary>
        [FormerlySerializedAs("_entries"), Header("Entries"), Tooltip("ScriptableObject entries (blocks, items, etc.) that belong to this tag"), SerializeField]
         private List<ScriptableObject> entries = new();

        /// <summary>ResourceId strings for entries that cannot be referenced by SO (e.g. cross-mod ids).</summary>
        [FormerlySerializedAs("_entryIds"), Tooltip("String entry ids (for backward compatibility or cross-reference)"), SerializeField]
         private List<string> entryIds = new();

        /// <summary>Resource-id namespace (typically "lithforge").</summary>
        public string Namespace
        {
            get { return @namespace; }
        }

        /// <summary>Slash-delimited tag name, e.g. "blocks/mineable_pickaxe".</summary>
        public string TagName
        {
            get { return tagName; }
        }

        /// <summary>Whether this tag replaces previously registered entries instead of appending.</summary>
        public bool Replace
        {
            get { return replace; }
        }

        /// <summary>Direct SO references that belong to this tag.</summary>
        public IReadOnlyList<ScriptableObject> Entries
        {
            get { return entries; }
        }

        /// <summary>ResourceId strings for entries referenced by id rather than SO.</summary>
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
