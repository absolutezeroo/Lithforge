using System;
using System.Collections;
using System.Collections.Generic;

using Lithforge.Core.Validation;
using Lithforge.Runtime.Bootstrap;
using Lithforge.Runtime.Debug;
using Lithforge.Runtime.Debug.Benchmark;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Storage;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Drives a single game session lifecycle as a coroutine:
    ///     content load → subsystem init → run → save → dispose.
    /// </summary>
    public static class SessionOrchestrator
    {
        /// <summary>
        ///     Runs one complete game session. Yields between phases for
        ///     progressive loading UI updates.
        /// </summary>
        public static IEnumerator Run(
            SessionConfig config,
            AppContext app,
            Action onSessionEnded)
        {
            GameSession session = null;

            Application.runInBackground = true;

            // Create loading screen
            PanelSettings panelSettings =
                Resources.Load<PanelSettings>("DefaultPanelSettings");
            GameObject loadingObject = new("LoadingScreen");
            LoadingScreen loadingScreen = loadingObject.AddComponent<LoadingScreen>();
            loadingScreen.Initialize(panelSettings);

            ContentValidator validator = new();
            ContentPipeline pipeline = new(
                app.Logger, validator, app.Settings.Rendering.AtlasTileSize);

            foreach (string phase in pipeline.Build())
            {
                loadingScreen.SetContentPhase(phase);
                yield return null;
            }

            ContentPipelineResult content = pipeline.Result;

            if (content == null)
            {
                UnityEngine.Debug.LogError(
                    "[Lithforge] Content pipeline failed — Result is null. Aborting session.");
                Object.Destroy(loadingObject);
                onSessionEnded?.Invoke();
                yield break;
            }

            UnityEngine.Debug.Log(
                $"[Lithforge] Content pipeline: {content.StateRegistry.TotalStateCount} states, " +
                $"{content.NativeAtlasLookup.TextureCount} textures, " +
                $"{content.BiomeDefinitions.Length} biomes, " +
                $"{content.OreDefinitions.Length} ores, " +
                $"{content.ItemEntries.Count} items, " +
                $"{content.LootTables.Count} loot tables, " +
                $"{content.TagRegistry.TagCount} tags.");

            try
            {
                session = new GameSession();

                // Store loading screen and panel settings on context for subsystems
                // that need them (SpawnSubsystem, UI subsystems)
                SessionInitArgs initArgs = new()
                {
                    LoadingScreen = loadingScreen, PanelSettings = panelSettings,
                };
                SessionInitArgsHolder.Current = initArgs;

                session.Initialize(config, app, content);
                app.CurrentSession = session;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Lithforge] Session initialization failed: {ex}");
                session?.Dispose();
                app.CurrentSession = null;
                Object.Destroy(loadingObject);
                SessionInitArgsHolder.Current = null;
                onSessionEnded?.Invoke();
                yield break;
            }

            UnityEngine.Debug.Log("[Lithforge] Session initialized.");

            SessionBridge bridge = null;

            if (session.Context.TryGet(out SessionBridge b))
            {
                bridge = b;
            }

            while (bridge == null || !bridge.QuitRequested)
            {
                yield return null;
            }

            yield return SaveAndDispose(session, app, panelSettings);

            app.CurrentSession = null;
            SessionInitArgsHolder.Current = null;
            onSessionEnded?.Invoke();
        }

        private static IEnumerator SaveAndDispose(
            GameSession session,
            AppContext app,
            PanelSettings panelSettings)
        {
            // Create saving screen overlay
            GameObject savingObject = new("SavingScreen");
            SavingScreen savingScreen = savingObject.AddComponent<SavingScreen>();
            savingScreen.Initialize(panelSettings);

            SaveProgress progress = new()
            {
                Phase = SaveState.CompletingJobs,
            };
            savingScreen.SetProgress(progress);
            yield return null;

            // Phase 1: Complete in-flight jobs
            try
            {
                session.Shutdown();

                if (session.Context.TryGet(out AutoSaveManager autoSave))
                {
                    autoSave.SaveMetadataOnly();
                }

                if (session.Context.TryGet(out AsyncChunkSaver asyncSaver))
                {
                    asyncSaver.Flush();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Lithforge] Error completing jobs during save: {ex}");
            }

            yield return null;

            // Phase 2: Save dirty chunks incrementally
            progress.Phase = SaveState.SavingChunks;

            if (session.Context.TryGet(out WorldStorage worldStorage)
                && session.Context.TryGet(out ChunkManager chunkManager))
            {
                List<ManagedChunk> dirtyChunks = new();
                chunkManager.CollectDirtyChunks(dirtyChunks);
                progress.TotalChunks = dirtyChunks.Count;
                progress.SavedChunks = 0;
                savingScreen.SetProgress(progress);

                for (int i = 0; i < dirtyChunks.Count; i++)
                {
                    ManagedChunk chunk = dirtyChunks[i];

                    try
                    {
                        chunk.ActiveJobHandle.Complete();
                        worldStorage.SaveChunk(
                            chunk.Coord, chunk.Data, chunk.LightData, chunk.BlockEntities);
                        chunk.IsDirty = false;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError(
                            $"[Lithforge] Error saving chunk {chunk.Coord}: {ex}");
                    }

                    progress.SavedChunks = i + 1;
                    savingScreen.SetProgress(progress);

                    if ((i + 1) % 8 == 0)
                    {
                        yield return null;
                    }
                }

                // Phase 3: Flush dirty regions
                progress.Phase = SaveState.FlushingRegions;

                List<RegionFile> dirtyRegions = new();
                worldStorage.CollectDirtyRegions(dirtyRegions);
                progress.TotalRegions = dirtyRegions.Count;
                progress.FlushedRegions = 0;
                savingScreen.SetProgress(progress);

                for (int i = 0; i < dirtyRegions.Count; i++)
                {
                    worldStorage.FlushRegion(dirtyRegions[i]);
                    progress.FlushedRegions = i + 1;
                    savingScreen.SetProgress(progress);
                    yield return null;
                }
            }

            progress.Phase = SaveState.Done;
            savingScreen.SetProgress(progress);

            Object.Destroy(savingObject);

            // Dispose the session (reverse-order subsystem disposal)
            session.Dispose();

            // Dispose content pipeline native resources
            DisposeContentResources(session.Context.Content);

            // Destroy session GameObjects and components on the bootstrap GO
            DestroySessionGameObjects();
            DestroySessionComponents(app.CoroutineHost.gameObject);

            // Reset screen manager state
            app.ScreenManager.OnEscapeEmpty = null;
            app.ScreenManager.ClearAll();
        }

        /// <summary>
        ///     Disposes native resources owned by the content pipeline result.
        /// </summary>
        internal static void DisposeContentResources(ContentPipelineResult content)
        {
            if (content == null)
            {
                return;
            }

            try
            {
                if (content.NativeStateRegistry.States.IsCreated)
                {
                    content.NativeStateRegistry.Dispose();
                }

                content.NativeAtlasLookup.Dispose();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"[Lithforge] Error disposing content resources: {ex}");
            }
        }

        /// <summary>
        ///     Destroys session-scoped GameObjects that were created by subsystems.
        /// </summary>
        private static void DestroySessionGameObjects()
        {
            string[] sessionObjectNames =
            {
                "Player",
                "BlockHighlight",
                "CrosshairHUD",
                "HotbarDisplay",
                "PlayerInventoryScreen",
                "ContainerScreenManager",
                "SettingsScreen",
                "PauseMenuScreen",
                "ChestScreen",
                "FurnaceScreen",
                "ToolStationScreen",
                "CraftingTableScreen",
                "PartBuilderScreen",
                "SavingScreen",
                "SfxSourcePool",
                "SessionBridge",
                "LoadingScreen",
            };

            for (int i = 0; i < sessionObjectNames.Length; i++)
            {
                GameObject obj = GameObject.Find(sessionObjectNames[i]);

                if (obj != null)
                {
                    Object.Destroy(obj);
                }
            }
        }

        /// <summary>
        ///     Removes session-scoped MonoBehaviour components from the bootstrap GameObject.
        /// </summary>
        private static void DestroySessionComponents(GameObject host)
        {
            DestroyComponent<TimeOfDayController>(host);
            DestroyComponent<SkyController>(host);
            DestroyComponent<ChunkBorderRenderer>(host);
            DestroyComponent<F3DebugOverlay>(host);
            DestroyComponent<BenchmarkRunner>(host);
        }

        private static void DestroyComponent<T>(GameObject host) where T : Component
        {
            T component = host.GetComponent<T>();

            if (component != null)
            {
                Object.Destroy(component);
            }
        }
    }

    /// <summary>
    ///     Transient arguments passed from <see cref="SessionOrchestrator" /> to
    ///     subsystems during initialization. Cleared after session init completes.
    /// </summary>
    public sealed class SessionInitArgs
    {
        public LoadingScreen LoadingScreen { get; set; }

        public PanelSettings PanelSettings { get; set; }
    }

    /// <summary>
    ///     Thread-local holder for <see cref="SessionInitArgs" /> during session init.
    /// </summary>
    public static class SessionInitArgsHolder
    {
        public static SessionInitArgs Current { get; set; }
    }
}
