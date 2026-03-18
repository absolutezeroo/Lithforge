using System.Collections.Generic;

using Lithforge.Runtime.Bootstrap;
using Lithforge.Runtime.Content.Blocks;
using Lithforge.Voxel.Block;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Shared utility for resolving block names to <see cref="StateId" /> values
    ///     from the content pipeline result. Used by multiple subsystems during initialization.
    /// </summary>
    public static class StateIdHelper
    {
        public static StateId FindStateIdForBlock(
            ContentPipelineResult content, BlockDefinition blockDef)
        {
            if (blockDef == null)
            {
                return StateId.Air;
            }

            return FindStateId(content, blockDef.Namespace + ":" + blockDef.BlockName);
        }

        public static StateId FindStateId(ContentPipelineResult content, string idString)
        {
            if (string.IsNullOrEmpty(idString) || !idString.Contains(':'))
            {
                UnityEngine.Debug.LogWarning(
                    $"[Lithforge] Invalid block id '{idString}', returning AIR.");
                return StateId.Air;
            }

            string[] parts = idString.Split(':');
            string ns = parts[0];
            string name = parts[1];

            IReadOnlyList<StateRegistryEntry> entries = content.StateRegistry.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                StateRegistryEntry entry = entries[i];

                if (entry.Id.Namespace == ns && entry.Id.Name == name)
                {
                    return new StateId(entry.BaseStateId);
                }
            }

            UnityEngine.Debug.LogWarning(
                $"[Lithforge] Block '{idString}' not found, returning AIR.");

            return StateId.Air;
        }
    }
}
