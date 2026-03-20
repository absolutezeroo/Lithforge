using System;
using System.Collections.Generic;

using Lithforge.Runtime.World;

using UnityEngine;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Holds the 3 voxel materials (opaque, cutout, translucent).
    /// </summary>
    public sealed class MaterialSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Materials";
            }
        }

        /// <summary>No dependencies.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the three voxel materials (opaque, cutout, translucent) and assigns the texture atlas.</summary>
        public void Initialize(SessionContext context)
        {
            Material opaqueMaterial = context.App.VoxelMaterial;

            if (opaqueMaterial == null)
            {
                Shader shader = Shader.Find("Lithforge/VoxelOpaque")
                                ?? Shader.Find("Lithforge/VoxelUnlit");

                if (shader != null)
                {
                    opaqueMaterial = new Material(shader);
                }
                else
                {
                    Shader fallback = Shader.Find("Universal Render Pipeline/Lit")
                                      ?? Shader.Find("Hidden/InternalErrorShader");
                    context.App.Logger.LogError(
                        "[Lithforge] VoxelOpaque shader not found! Using fallback.");
                    opaqueMaterial = new Material(fallback);
                }
            }

            if (context.Content.AtlasResult?.TextureArray != null)
            {
                opaqueMaterial.SetTexture("_AtlasArray", context.Content.AtlasResult.TextureArray);
            }

            // Cutout material
            Material cutoutMaterial;
            Shader cutoutShader = Shader.Find("Lithforge/VoxelCutout");

            if (cutoutShader != null)
            {
                cutoutMaterial = new Material(cutoutShader);
            }
            else
            {
                context.App.Logger.LogWarning("[Lithforge] VoxelCutout shader not found, using opaque fallback.");
                cutoutMaterial = new Material(opaqueMaterial);
            }

            if (context.Content.AtlasResult?.TextureArray != null)
            {
                cutoutMaterial.SetTexture("_AtlasArray", context.Content.AtlasResult.TextureArray);
            }

            // Translucent material
            Material translucentMaterial;
            Shader translucentShader = Shader.Find("Lithforge/VoxelTranslucent");

            if (translucentShader != null)
            {
                translucentMaterial = new Material(translucentShader);
            }
            else
            {
                context.App.Logger.LogWarning("[Lithforge] VoxelTranslucent shader not found, using opaque fallback.");
                translucentMaterial = new Material(opaqueMaterial);
            }

            if (context.Content.AtlasResult?.TextureArray != null)
            {
                translucentMaterial.SetTexture("_AtlasArray", context.Content.AtlasResult.TextureArray);
            }

            VoxelMaterials materials = new(opaqueMaterial, cutoutMaterial, translucentMaterial);
            context.Register(materials);
        }

        /// <summary>No post-initialization wiring needed.</summary>
        public void PostInitialize(SessionContext context)
        {
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned disposable resources; materials are cleaned up with the scene.</summary>
        public void Dispose()
        {
        }
    }

    /// <summary>Simple holder for the three voxel materials.</summary>
    public sealed class VoxelMaterials
    {
        /// <summary>Creates a VoxelMaterials holder with the three render-layer materials.</summary>
        public VoxelMaterials(Material opaque, Material cutout, Material translucent)
        {
            Opaque = opaque;
            Cutout = cutout;
            Translucent = translucent;
        }
        /// <summary>Material for opaque solid blocks.</summary>
        public Material Opaque { get; }

        /// <summary>Material for cutout blocks (leaves, flowers) with alpha test.</summary>
        public Material Cutout { get; }

        /// <summary>Material for translucent blocks (water, glass) with alpha blending.</summary>
        public Material Translucent { get; }
    }
}
