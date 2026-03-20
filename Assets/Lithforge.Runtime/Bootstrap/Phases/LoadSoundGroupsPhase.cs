using Lithforge.Runtime.Audio;
using Lithforge.Runtime.Content.Tools;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Phase 18: Loads SoundGroupDefinition ScriptableObjects and creates the ToolTemplateRegistry.</summary>
    public sealed class LoadSoundGroupsPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Loading sound groups...";
            }
        }

        /// <summary>Loads sound group assets, registers them by name, and creates an empty ToolTemplateRegistry.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            SoundGroupRegistry soundGroupRegistry = new(ctx.Logger);
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
