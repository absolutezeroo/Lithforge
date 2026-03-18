using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class AutoSaveSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "AutoSave";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(StorageSubsystem),
            typeof(PlayerSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.HasLocalWorld;
        }

        public void Initialize(SessionContext context)
        {
            WorldStorage worldStorage = context.Get<WorldStorage>();
            WorldMetadata metadata = context.Get<WorldMetadata>();
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();

            AutoSaveManager autoSave = new(
                worldStorage,
                metadata,
                player.Transform,
                player.MainCamera,
                () => 0f, // TimeOfDay wired in PostInitialize
                player.Inventory);

            if (context.TryGet(out AsyncChunkSaver saver))
            {
                autoSave.SetAsyncSaver(saver);
            }

            context.Register(autoSave);
        }

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
            // AutoSaveManager.SaveMetadataOnly called by SessionOrchestrator
        }

        public void Dispose()
        {
        }
    }
}
