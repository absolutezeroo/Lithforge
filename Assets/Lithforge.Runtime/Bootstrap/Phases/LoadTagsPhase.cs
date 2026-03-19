using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Tags;
using Lithforge.Voxel.Tag;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Phase 10: Loads Tag ScriptableObjects and builds the bidirectional TagRegistry.</summary>
    public sealed class LoadTagsPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Loading tags...";
            }
        }

        /// <summary>Loads tag assets from Resources and builds the bidirectional TagRegistry.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            Tag[] tagAssets = Resources.LoadAll<Tag>("Content/Tags");
            TagRegistry tagRegistry = new();

            for (int i = 0; i < tagAssets.Length; i++)
            {
                Tag tag = tagAssets[i];
                ResourceId tagId = new(tag.Namespace, tag.TagName);
                TagDefinition tagDef = new(tagId)
                {
                    Replace = tag.Replace,
                };

                IReadOnlyList<string> entryIds = tag.EntryIds;

                for (int e = 0; e < entryIds.Count; e++)
                {
                    tagDef.Values.Add(entryIds[e]);
                }

                tagRegistry.Register(tagDef);
            }

            ctx.TagRegistry = tagRegistry;
            ctx.Logger.LogInfo($"Loaded {tagAssets.Length} tags, {tagRegistry.TagCount} unique.");
        }
    }
}
