using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Runtime.Content.Items;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class LoadItemsPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Loading items...";
            }
        }

        public void Execute(ContentPhaseContext ctx)
        {
            ctx.Items = Resources.LoadAll<ItemDefinition>("Content/Items");
            ctx.Logger.LogInfo($"Loaded {ctx.Items.Length} item definitions.");

            List<ItemEntry> itemEntries = new();

            for (int i = 0; i < ctx.Items.Length; i++)
            {
                ItemEntry itemDef = ConvertItem(ctx.Items[i]);
                itemEntries.Add(itemDef);
            }

            ctx.ItemEntries = itemEntries;
        }

        private static ItemEntry ConvertItem(ItemDefinition item)
        {
            ResourceId id = new(item.Namespace, item.ItemName);
            ItemEntry def = new(id)
            {
                MaxStackSize = item.MaxStackSize, FuelTime = item.FuelTime,
            };

            if (item.PlacesBlock != null)
            {
                def.IsBlockItem = true;
                def.BlockId = new ResourceId(
                    item.PlacesBlock.Namespace, item.PlacesBlock.BlockName);
            }

            IReadOnlyList<string> tags = item.Tags;

            for (int i = 0; i < tags.Count; i++)
            {
                def.Tags.Add(tags[i]);
            }

            return def;
        }
    }
}
