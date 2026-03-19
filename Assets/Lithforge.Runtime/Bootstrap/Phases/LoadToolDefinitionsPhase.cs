using Lithforge.Runtime.Content.Tools;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    /// <summary>Loads ToolDefinition ScriptableObjects that define tool types and their part layouts.</summary>
    public sealed class LoadToolDefinitionsPhase : IContentPhase
    {
        /// <summary>Loading screen description.</summary>
        public string Description
        {
            get
            {
                return "Loading tool definitions...";
            }
        }

        /// <summary>Loads tool definition assets from Resources into the content phase context.</summary>
        public void Execute(ContentPhaseContext ctx)
        {
            ctx.ToolDefinitions =
                Resources.LoadAll<ToolDefinition>("Content/ToolDefinitions");

            if (ctx.ToolDefinitions.Length == 0)
            {
                ctx.Logger.LogWarning(
                    "No ToolDefinition assets found in Content/ToolDefinitions/. Tool sprite compositing disabled.");
            }
            else
            {
                ctx.Logger.LogInfo($"Loaded {ctx.ToolDefinitions.Length} tool definitions.");
            }
        }
    }
}
