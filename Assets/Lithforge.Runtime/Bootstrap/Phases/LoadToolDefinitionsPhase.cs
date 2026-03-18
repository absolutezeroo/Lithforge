using Lithforge.Runtime.Content.Tools;

using UnityEngine;

namespace Lithforge.Runtime.Bootstrap.Phases
{
    public sealed class LoadToolDefinitionsPhase : IContentPhase
    {
        public string Description
        {
            get
            {
                return "Loading tool definitions...";
            }
        }

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
