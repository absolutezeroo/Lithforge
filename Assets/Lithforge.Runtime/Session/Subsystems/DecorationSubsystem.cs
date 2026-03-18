using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.WorldGen.Decoration;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class DecorationSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "Decoration";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(WorldGenSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld && config is not SessionConfig.Client;
        }

        public void Initialize(SessionContext context)
        {
            NativeBiomeDataHolder biomeHolder = context.Get<NativeBiomeDataHolder>();
            WorldGenSettings wg = context.App.Settings.WorldGen;

            StateId oakLogId = StateIdHelper.FindStateId(context.Content, "lithforge:oak_log");
            StateId oakLeavesId = StateIdHelper.FindStateId(context.Content, "lithforge:oak_leaves");
            StateId airId = StateId.Air;

            DecorationStage decoration = new(biomeHolder.Data, oakLogId, oakLeavesId, airId, wg.SeaLevel);
            context.Register(decoration);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
