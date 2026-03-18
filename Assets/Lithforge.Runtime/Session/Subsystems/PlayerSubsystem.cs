using System;
using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Item;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Player;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Item;
using Lithforge.Voxel.Storage;

using Unity.Mathematics;

using UnityEngine;

using Object = UnityEngine.Object;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Simple holder for arm material references shared with RemotePlayerManagerSubsystem.</summary>
    public sealed class ArmMaterials
    {
        public ArmMaterials(Material baseMat, Material overlayMat, Material heldItemMat)
        {
            Base = baseMat;
            Overlay = overlayMat;
            HeldItem = heldItemMat;
        }
        public Material Base { get; }

        public Material Overlay { get; }

        public Material HeldItem { get; }
    }

    /// <summary>Holds the player's Transform for registration in SessionContext.</summary>
    public sealed class PlayerTransformHolder
    {
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
        public Transform Transform { get; }

        public Camera MainCamera { get; }

        public PlayerController Controller { get; }

        public Inventory Inventory { get; }

        public PlayerPhysicsBody PhysicsBody { get; set; }

        public PlayerRenderer Renderer { get; set; }

        public bool HasRestoredState { get; }

        public float RestoredTimeOfDay { get; }
    }

    public sealed class PlayerSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "Player";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(ChunkManagerSubsystem),
            typeof(PlayerPhysicsSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

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

            if (context.TryGet(out WorldMetadata metadata)
                && metadata.PlayerState != null && !isNewWorld)
            {
                PlayerStateSerializer.Restore(
                    metadata.PlayerState,
                    playerObject.transform,
                    mainCamera,
                    playerInventory,
                    context.Content.ItemRegistry,
                    out restoredTimeOfDay);
                hasRestoredState = true;
                UnityEngine.Debug.Log("[Lithforge] Player state restored from save.");
            }
            else
            {
                IReadOnlyList<StartingItemEntry> startingItems = gameplay.StartingItems;

                for (int i = 0; i < startingItems.Count; i++)
                {
                    StartingItemEntry entry = startingItems[i];
                    ResourceId itemId = new(entry.itemNamespace, entry.itemName);
                    ItemEntry itemDef = context.Content.ItemRegistry.Get(itemId);
                    int maxStack = itemDef != null
                        ? itemDef.MaxStackSize
                        : physics.DefaultMaxStackSize;

                    byte[] toolData = context.Content.ToolTemplateRegistry.GetTemplate(itemId);

                    if (toolData != null)
                    {
                        ToolInstance toolTemplate = ToolInstanceSerializer.Deserialize(toolData);
                        int durability = toolTemplate != null ? toolTemplate.MaxDurability : -1;
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

            // Create physics body
            PlayerPhysicsManager physicsManager = context.Get<PlayerPhysicsManager>();
            float3 startPos = new(
                playerObject.transform.position.x,
                playerObject.transform.position.y,
                playerObject.transform.position.z);
            PlayerPhysicsBody physicsBody = physicsManager.AddPlayer(0, startPos, physics);
            playerController.SetPhysicsBody(physicsBody);

            // Create InputSnapshotBuilder
            InputSnapshotBuilder inputBuilder = new(playerObject.transform, mainCamera.transform);
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
                UnityEngine.Debug.LogWarning(
                    "[Lithforge] PlayerModel shaders not found. Player model will not render.");
            }

            // Add BlockHighlight
            GameObject highlightObject = new("BlockHighlight");
            BlockHighlight blockHighlight = highlightObject.AddComponent<BlockHighlight>();
            blockHighlight.Initialize(rendering);

            // Register everything
            PlayerTransformHolder holder = new(
                playerObject.transform, mainCamera, playerController,
                playerInventory, physicsBody, hasRestoredState, restoredTimeOfDay);
            holder.Renderer = playerRenderer;

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

        public void PostInitialize(SessionContext context)
        {
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
            // Player GameObjects destroyed in session cleanup
        }
    }
}
