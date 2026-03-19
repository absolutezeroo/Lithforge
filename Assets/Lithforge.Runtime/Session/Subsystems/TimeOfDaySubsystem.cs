using System;
using System.Collections.Generic;

using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the time-of-day controller and procedural sky for the day/night cycle.</summary>
    public sealed class TimeOfDaySubsystem : IGameSubsystem
    {
        /// <summary>The owned procedural sky controller.</summary>
        private SkyController _skyController;

        /// <summary>The owned time-of-day controller driving sun light and material updates.</summary>
        private TimeOfDayController _timeOfDay;

        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "TimeOfDay";
            }
        }

        /// <summary>Depends on mesh store for materials and player for restored state.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkMeshStoreSubsystem),
            typeof(PlayerSubsystem),
        };

        /// <summary>Only created for sessions that render chunks.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the time-of-day controller, sky controller, and restores saved time.</summary>
        public void Initialize(SessionContext context)
        {
            ChunkMeshStore meshStore = context.Get<ChunkMeshStore>();
            Material material = meshStore.OpaqueMaterial;

            if (material == null)
            {
                return;
            }

            MonoBehaviour host = context.App.CoroutineHost;
            RenderingSettings rendering = context.App.Settings.Rendering;

            _timeOfDay = host.gameObject.AddComponent<TimeOfDayController>();
            _timeOfDay.Initialize(
                material,
                meshStore.CutoutMaterial,
                meshStore.TranslucentMaterial,
                rendering);

            // Restore time of day from saved state
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();

            if (player.HasRestoredState)
            {
                _timeOfDay.SetTimeOfDay(player.RestoredTimeOfDay);
            }

            // Register arm materials for day/night cycle updates
            if (context.TryGet(out ArmMaterials armMats))
            {
                _timeOfDay.RegisterMaterial(armMats.Base);
                _timeOfDay.RegisterMaterial(armMats.Overlay);
                _timeOfDay.RegisterMaterial(armMats.HeldItem);
            }

            // Initialize procedural sky
            ChunkManager chunkManager = context.Get<ChunkManager>();
            _skyController = host.gameObject.AddComponent<SkyController>();
            _skyController.Initialize(
                _timeOfDay,
                _timeOfDay.DirectionalLight,
                rendering,
                chunkManager);

            context.Register(_timeOfDay);
            context.Register(_skyController);
        }

        /// <summary>Registers the time-of-day tick adapter in the fixed-rate tick loop.</summary>
        public void PostInitialize(SessionContext context)
        {
            // Register time-of-day adapter in the fixed tick loop
            if (context.TryGet(out TickRegistry registry) && _timeOfDay != null)
            {
                registry.Register(new TimeOfDayTickAdapter(_timeOfDay));
            }
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>MonoBehaviour controllers are destroyed with the bootstrap GameObject.</summary>
        public void Dispose()
        {
            // TimeOfDayController and SkyController are MonoBehaviours,
            // destroyed when the bootstrap GameObject is cleaned up.
        }
    }
}
