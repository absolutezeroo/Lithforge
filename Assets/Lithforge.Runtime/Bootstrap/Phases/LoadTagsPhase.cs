using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Tags;
using Lithforge.Voxel.Tag;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class LoadTagsPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Loading tags...";
            }
        }

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
