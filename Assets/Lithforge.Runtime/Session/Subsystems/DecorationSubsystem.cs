using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.WorldGen.Decoration;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the decoration stage for tree placement after chunk generation.</summary>
    public sealed class DecorationSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Decoration";
            }
        }

        /// <summary>Depends on world gen for biome data.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(WorldGenSubsystem),
        };

        /// <summary>Only created for sessions with local world generation (not pure clients).</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld && config is not SessionConfig.Client;
        }

        /// <summary>Creates the decoration stage with biome data and tree block state IDs.</summary>
        public void Initialize(SessionContext context)
        {
            NativeBiomeDataHolder biomeHolder = context.Get<NativeBiomeDataHolder>();
            WorldGenSettings wg = context.App.Settings.WorldGen;

            StateId oakLogId = StateIdHelper.FindStateId(context.Content, "lithforge:oak_log", context.App.Logger);
            StateId oakLeavesId = StateIdHelper.FindStateId(context.Content, "lithforge:oak_leaves", context.App.Logger);
            StateId airId = StateId.Air;

            DecorationStage decoration = new(biomeHolder.Data, oakLogId, oakLeavesId, airId, wg.SeaLevel);
            context.Register(decoration);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned disposable resources.</summary>
        public void Dispose()
        {
        }
    }
}
