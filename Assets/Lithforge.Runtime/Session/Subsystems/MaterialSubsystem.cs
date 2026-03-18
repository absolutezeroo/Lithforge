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
        public string Name
        {
            get
            {
                return "Materials";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = Array.Empty<Type>();

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

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
                    UnityEngine.Debug.LogError(
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
                UnityEngine.Debug.LogWarning("[Lithforge] VoxelCutout shader not found, using opaque fallback.");
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
                UnityEngine.Debug.LogWarning("[Lithforge] VoxelTranslucent shader not found, using opaque fallback.");
                translucentMaterial = new Material(opaqueMaterial);
            }

            if (context.Content.AtlasResult?.TextureArray != null)
            {
                translucentMaterial.SetTexture("_AtlasArray", context.Content.AtlasResult.TextureArray);
            }

            VoxelMaterials materials = new(opaqueMaterial, cutoutMaterial, translucentMaterial);
            context.Register(materials);
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

    /// <summary>Simple holder for the three voxel materials.</summary>
    public sealed class VoxelMaterials
    {
        public VoxelMaterials(Material opaque, Material cutout, Material translucent)
        {
            Opaque = opaque;
            Cutout = cutout;
            Translucent = translucent;
        }
        public Material Opaque { get; }
        public Material Cutout { get; }
        public Material Translucent { get; }
    }
}
