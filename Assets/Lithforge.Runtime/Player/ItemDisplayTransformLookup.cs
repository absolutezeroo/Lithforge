using System.Collections.Generic;

using Lithforge.Core.Data;
using Lithforge.Runtime.Content.Models;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Player
{
    /// <summary>
    ///     Lookup table mapping item ResourceIds to their first-person right hand display transforms.
    ///     Built during the content pipeline from resolved BlockModel parent chains.
    /// </summary>
    public sealed class ItemDisplayTransformLookup
    {
        /// <summary>Map of item ResourceId to its resolved display transform matrix.</summary>
        private readonly Dictionary<ResourceId, float4x4> _transforms = new();

        /// <summary>
        ///     Registers a resolved display transform matrix for an item.
        /// </summary>
        public void Register(ResourceId itemId, float4x4 displayMatrix)
        {
            _transforms[itemId] = displayMatrix;
        }

        /// <summary>
        ///     Gets the display transform matrix for an item.
        ///     Returns identity if no display transform is registered.
        /// </summary>
        public float4x4 Get(ResourceId itemId)
        {
            if (_transforms.TryGetValue(itemId, out float4x4 mat))
            {
                return mat;
            }

            return float4x4.identity;
        }

        /// <summary>
        ///     Builds a display transform matrix from a ModelDisplayTransform.
        ///     Minecraft transform order: Translate → RotateY → RotateX → RotateZ → Scale.
        ///     Translation units are 1/16 of a block.
        /// </summary>
        public static float4x4 BuildMatrix(ModelDisplayTransform dt)
        {
            if (dt == null || !dt.HasValue)
            {
                return float4x4.identity;
            }

            Vector3 rot = dt.Rotation;
            Vector3 trans = dt.Translation;
            Vector3 scl = dt.Scale;

            // Translation in 1/16 block units → block units
            float4x4 translate = float4x4.Translate(new float3(trans.x, trans.y, trans.z) / 16f);

            // Rotation order: Y → X → Z (Minecraft convention)
            float4x4 rotY = float4x4.RotateY(math.radians(rot.y));
            float4x4 rotX = float4x4.RotateX(math.radians(rot.x));
            float4x4 rotZ = float4x4.RotateZ(math.radians(rot.z));

            // Scale (uniform or per-axis)
            float4x4 scale = float4x4.Scale(new float3(scl.x, scl.y, scl.z));

            // Translate → RotateY → RotateX → RotateZ → Scale
            return math.mul(
                math.mul(translate, math.mul(rotY, math.mul(rotX, rotZ))),
                scale);
        }
    }
}
