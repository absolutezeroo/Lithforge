using System;
using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Storage;

using UnityEngine;

using Object = UnityEngine.Object;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Simple holder for arm material references shared with RemotePlayerManagerSubsystem.</summary>
    public sealed class ArmMaterials
    {
        /// <summary>Creates an ArmMaterials holder with the three arm rendering materials.</summary>
        public ArmMaterials(Material baseMat, Material overlayMat, Material heldItemMat)
        {
            Base = baseMat;
            Overlay = overlayMat;
            HeldItem = heldItemMat;
        }
        /// <summary>Base skin-layer material for the player model.</summary>
        public Material Base { get; }

        /// <summary>Overlay-layer material for the player model (hat, jacket, etc.).</summary>
        public Material Overlay { get; }

        /// <summary>Material for the held item rendered in the player's hand.</summary>
        public Material HeldItem { get; }
    }

    /// <summary>Holds the player's Transform for registration in SessionContext.</summary>
    public sealed class PlayerTransformHolder
    {
        /// <summary>Creates a player transform holder with all player-related references.</summary>
        public PlayerTransformHolder(
            Transform transform, Camera mainCamera, PlayerController controller,
            Inventory inventory, PlayerPhysicsBody physicsBody,
            bool hasRestoredState, float restoredTimeOfDay)
        {
            Transform = transform;
            MainCamera = mainCamera;
            Controller = controller;
            Inventory = inventory;
            PhysicsBody = physicsBody;
            HasRestoredState = hasRestoredState;
            RestoredTimeOfDay = restoredTimeOfDay;
        }
        /// <summary>The player's root Transform for position and rotation.</summary>
        public Transform Transform { get; }

        /// <summary>The main camera attached under the player hierarchy.</summary>
        public Camera MainCamera { get; }

        /// <summary>The player input controller component.</summary>
        public PlayerController Controller { get; }

        /// <summary>The player's 36-slot inventory.</summary>
        public Inventory Inventory { get; }

        /// <summary>The player's physics body for collision detection; may be swapped after handshake.</summary>
        public PlayerPhysicsBody PhysicsBody { get; set; }

        /// <summary>The player model renderer for first-person arms and third-person body.</summary>
        public PlayerRenderer Renderer { get; set; }

        /// <summary>Whether the player state was restored from a save file.</summary>
        public bool HasRestoredState { get; }

        /// <summary>The time-of-day value restored from the save file (0-1).</summary>
        public float RestoredTimeOfDay { get; }
    }

    /// <summary>
    ///     Subsystem that creates the player object, camera, controller, physics body,
    ///     inventory, renderer, and block highlight.
    /// </summary>
    public sealed class PlayerSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Player";
            }
        }

        /// <summary>Depends on chunk manager for world data access.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the player hierarchy, camera, controller, inventory, renderer, and highlight.</summary>
        public void Initialize(SessionContext context)
        {
            PhysicsSettings physics = context.App.Settings.Physics;
            RenderingSettings rendering = context.App.Settings.Rendering;
            WorldGenSettings worldGen = context.App.Settings.WorldGen;
            ChunkSettings chunk = context.App.Settings.Chunk;
            GameplaySettings gameplay = context.App.Settings.Gameplay;

            Camera mainCamera = Camera.main;

            if (mainCamera == null)
            {
                return;
            }

            // Remove legacy FPSCameraController if present
            FPSCameraController legacyController = mainCamera.GetComponent<FPSCameraController>();

            if (legacyController != null)
            {
                Object.Destroy(legacyController);
            }

            // Create player parent object
            GameObject playerObject = new("Player")
            {
                transform =
                {
                    position = new Vector3(
                        0,
                        worldGen.SeaLevel + chunk.InitialSpawnYOffset,
                        0),
                },
            };

            // Reparent camera under player
            mainCamera.transform.SetParent(playerObject.transform);
            mainCamera.transform.localPosition = new Vector3(0f, physics.PlayerEyeHeight, 0f);
            mainCamera.transform.localRotation = Quaternion.Euler(rendering.InitialCameraPitch, 0f, 0f);
            mainCamera.nearClipPlane = rendering.NearClipPlane;
            mainCamera.farClipPlane = rendering.FarClipPlane;

            // Add PlayerController
            PlayerController playerController = playerObject.AddComponent<PlayerController>();
            playerController.Initialize();

            // Add CameraController
            CameraController cameraController = mainCamera.gameObject.AddComponent<CameraController>();

            // Create player inventory
            Inventory playerInventory = new();

            // Resolve player state
            bool hasRestoredState = false;
            float restoredTimeOfDay = 0f;

            bool isNewWorld = context.Config switch
            {
                SessionConfig.Singleplayer sp => sp.IsNewWorld,
                SessionConfig.Host host => host.IsNewWorld,
                _ => false,
            };

            // Try loading player state from per-player data store first, then fall back to legacy
            WorldPlayerState restoredPlayerState = null;

            if (!isNewWorld && context.TryGet(out PlayerDataStore playerDataStore))
            {
                restoredPlayerState = playerDataStore.Load("local");
            }

            if (restoredPlayerState is not null)
            {
                PlayerStateSerializer.Restore(
                    restoredPlayerState,
                    playerObject.transform,
                    mainCamera,
                    playerInventory,
                    context.Content.ItemRegistry,
                    out restoredTimeOfDay,
                    context.App.Logger);
                hasRestoredState = true;
                context.App.Logger.LogInfo("[Lithforge] Player state restored from playerdata/local.json.");
            }
            else
            {
                IReadOnlyList<StartingItemEntry> startingItems = gameplay.StartingItems;

                for (int i = 0; i < startingItems.Count; i++)
                {
                    StartingItemEntry entry = startingItems[i];
                    ResourceId itemId = new(entry.itemNamespace, entry.itemName);
                    ItemEntry itemDef = context.Content.ItemRegistry.Get(itemId);
                    int maxStack = itemDef?.MaxStackSize ?? physics.DefaultMaxStackSize;

                    byte[] toolData = context.Content.ToolTemplateRegistry.GetTemplate(itemId);

                    if (toolData != null)
                    {
                        ToolInstance toolTemplate = ToolInstanceSerializer.Deserialize(toolData);
                        int durability = toolTemplate?.MaxDurability ?? -1;
                        ItemStack toolStack = new(itemId, 1, durability);
                        DataComponentMap toolMap = new();
                        toolMap.Set(DataComponentTypes.ToolInstanceId,
                            new ToolInstanceComponent(toolTemplate));
                        toolStack.Components = toolMap;
                        playerInventory.AddItemStack(toolStack);
                    }
                    else
                    {
                        playerInventory.AddItem(itemId, entry.count, maxStack);
                    }
                }
            }

            // Create KeyBindingConfig from persisted preferences or defaults
            KeyBindingConfig keyBindings;
            UserPreferences prefs = context.App.UserPreferences;

            if (prefs.KeyBindingsJson is not null)
            {
                try
                {
                    Dictionary<string, string> dict =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(
                            prefs.KeyBindingsJson);
                    keyBindings = KeyBindingConfig.FromDictionary(dict);
                }
                catch
                {
                    keyBindings = new KeyBindingConfig();
                }
            }
            else
            {
                keyBindings = new KeyBindingConfig();
            }

            context.Register(keyBindings);

            // Create InputSnapshotBuilder
            InputSnapshotBuilder inputBuilder = new(playerObject.transform, mainCamera.transform, keyBindings);
            context.Register(inputBuilder);

            // Create arm materials and player renderer
            SkinLoader skinLoader = new();
            Texture2D skinTexture = skinLoader.LoadSkin("default.png") ?? skinLoader.CreateDefaultSkin();

            Shader armBaseShader = Shader.Find("Lithforge/PlayerModel");
            Shader armOverlayShader = Shader.Find("Lithforge/PlayerModelOverlay");
            Shader heldItemShader = Shader.Find("Lithforge/HeldItem");
            ArmMaterials armMats = null;
            PlayerRenderer playerRenderer = null;

            if (armBaseShader != null && armOverlayShader != null && heldItemShader != null)
            {
                Material armBaseMat = new(armBaseShader);
                Material armOverlayMat = new(armOverlayShader);
                Material heldItemMat = new(heldItemShader);
                armMats = new ArmMaterials(armBaseMat, armOverlayMat, heldItemMat);

                // Share atlas texture with held item material
                heldItemMat.SetTexture("_AtlasArray", context.Content.AtlasResult.TextureArray);

                playerRenderer = new PlayerRenderer(
                    armBaseMat, armOverlayMat, heldItemMat,
                    skinTexture,
                    playerObject.transform,
                    mainCamera.transform,
                    playerInventory,
                    context.Content.ItemRegistry,
                    context.Content.StateRegistry,
                    context.Content.DisplayTransformLookup);
            }
            else
            {
                context.App.Logger.LogWarning(
                    "[Lithforge] PlayerModel shaders not found. Player model will not render.");
            }

            // Add BlockHighlight
            GameObject highlightObject = new("BlockHighlight");
            BlockHighlight blockHighlight = highlightObject.AddComponent<BlockHighlight>();
            blockHighlight.Initialize(rendering);

            // Register everything (physics body is null — created later by ClientPlayerBodyFactory)
            PlayerTransformHolder holder = new(
                playerObject.transform, mainCamera, playerController,
                playerInventory, null, hasRestoredState, restoredTimeOfDay)
            {
                Renderer = playerRenderer,
            };

            context.Register(holder);
            context.Register(playerInventory);
            context.Register(playerController);
            context.Register(cameraController);
            context.Register(blockHighlight);

            if (armMats != null)
            {
                context.Register(armMats);
            }

            if (playerRenderer != null)
            {
                context.Register(playerRenderer);
            }
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Player GameObjects destroyed in session cleanup.</summary>
        public void Dispose()
        {
            // Player GameObjects destroyed in session cleanup
        }
    }
}
