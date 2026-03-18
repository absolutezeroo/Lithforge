using Lithforge.Runtime.Audio;
using Lithforge.Runtime.Content.Tools;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class LoadSoundGroupsPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Loading sound groups...";
            }
        }

        public void Execute(ContentPhaseContext ctx)
        {
            SoundGroupRegistry soundGroupRegistry = new();
            SoundGroupDefinition[] soundGroups =
                Resources.LoadAll<SoundGroupDefinition>("Content/SoundGroups");

            for (int i = 0; i < soundGroups.Length; i++)
            {
                SoundGroupDefinition sg = soundGroups[i];

                if (!string.IsNullOrEmpty(sg.GroupName))
                {
                    soundGroupRegistry.Register(sg.GroupName, sg);
                }
            }

            ctx.SoundGroupRegistry = soundGroupRegistry;
            ctx.Logger.LogInfo($"Loaded {soundGroupRegistry.Count} sound groups.");

            // Create tool template registry (empty for now — tool templates are populated
            // by mod/content packs in a future pipeline phase)
            ctx.ToolTemplateRegistry = new ToolTemplateRegistry(null);
        }
    }
}
