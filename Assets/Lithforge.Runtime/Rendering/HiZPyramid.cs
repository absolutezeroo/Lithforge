using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Manages a Hierarchical Z-Buffer mipmap pyramid for GPU occlusion culling.
    /// Generates the pyramid from <c>_CameraDepthTexture</c> (previous frame's depth)
    /// using compute shader 2x2 downsample passes.
    ///
    /// Each mip level stores the MIN depth (reversed-Z: farthest occluder surface),
    /// which is conservative for occlusion testing. A chunk is occluded if its nearest
    /// point is farther than the Hi-Z depth at that screen location.
    ///
    /// Uses separate RenderTextures per mip level to avoid read-write hazards on DX11.
    /// Owner: ChunkMeshStore. Lifetime: application session.
    /// </summary>
    public sealed class HiZPyramid : IDisposable
    {
        private static readonly int s_depthSourceId = Shader.PropertyToID("_DepthSource");
        private static readonly int s_hiZMip0Id = Shader.PropertyToID("_HiZMip0");
        private static readonly int s_hiZPrevMipId = Shader.PropertyToID("_HiZPrevMip");
        private static readonly int s_hiZNextMipId = Shader.PropertyToID("_HiZNextMip");
        private static readonly int s_copySizeId = Shader.PropertyToID("_CopySize");
        private static readonly int s_downsampleSizeId = Shader.PropertyToID("_DownsampleSize");

        private readonly ComputeShader _hiZShader;
        private readonly int _copyKernel;
        private readonly int _downsampleKernel;

        /// <summary>Per-mip-level RenderTextures. Index 0 = full resolution.</summary>
        private RenderTexture[] _mipTextures;

        private int _width;
        private int _height;
        private int _mipCount;
        private bool _isValid;

        /// <summary>Combined mipmapped RT for compute shader sampling in occlusion test.</summary>
        private RenderTexture _combinedTexture;

        /// <summary>Whether the pyramid has been generated this frame and is usable.</summary>
        public bool IsValid
        {
            get { return _isValid; }
        }

        /// <summary>Number of mip levels in the pyramid.</summary>
        public int MipCount
        {
            get { return _mipCount; }
        }

        public HiZPyramid(ComputeShader hiZShader)
        {
            _hiZShader = hiZShader;
            _copyKernel = hiZShader.FindKernel("CSHiZCopy");
            _downsampleKernel = hiZShader.FindKernel("CSHiZDownsample");
        }

        /// <summary>
        /// Generates the Hi-Z pyramid from the given depth texture.
        /// Called once per frame before occlusion culling.
        /// Recreates mip textures if screen resolution changed.
        /// </summary>
        public void Generate(Texture depthTexture)
        {
            _isValid = false;

            if (depthTexture == null)
            {
                return;
            }

            int texW = depthTexture.width;
            int texH = depthTexture.height;

            if (texW <= 0 || texH <= 0)
            {
                return;
            }

            // Recreate mip chain if resolution changed
            if (_mipTextures == null || texW != _width || texH != _height)
            {
                ReleaseMipTextures();
                CreateMipTextures(texW, texH);
            }

            // Mip 0: copy from depth source
            _hiZShader.SetTexture(_copyKernel, s_depthSourceId, depthTexture);
            _hiZShader.SetTexture(_copyKernel, s_hiZMip0Id, _mipTextures[0]);
            _hiZShader.SetInts(s_copySizeId, _width, _height);
            _hiZShader.Dispatch(_copyKernel, (_width + 7) / 8, (_height + 7) / 8, 1);

            // Subsequent mips: 2x2 downsample
            for (int mip = 1; mip < _mipCount; mip++)
            {
                int mipW = Mathf.Max(1, _width >> mip);
                int mipH = Mathf.Max(1, _height >> mip);

                _hiZShader.SetTexture(_downsampleKernel, s_hiZPrevMipId, _mipTextures[mip - 1]);
                _hiZShader.SetTexture(_downsampleKernel, s_hiZNextMipId, _mipTextures[mip]);
                _hiZShader.SetInts(s_downsampleSizeId, mipW, mipH);
                _hiZShader.Dispatch(_downsampleKernel, (mipW + 7) / 8, (mipH + 7) / 8, 1);
            }

            // Assemble per-mip RTs into combined mipmapped texture for compute sampling
            for (int mip = 0; mip < _mipCount; mip++)
            {
                Graphics.CopyTexture(_mipTextures[mip], 0, 0, _combinedTexture, 0, mip);
            }

            _isValid = true;
        }

        /// <summary>
        /// Returns the mip texture at the given level.
        /// </summary>
        public RenderTexture GetMipTexture(int mipLevel)
        {
            if (_mipTextures == null || mipLevel < 0 || mipLevel >= _mipCount)
            {
                return null;
            }

            return _mipTextures[mipLevel];
        }

        /// <summary>
        /// Returns the full-resolution mip 0 texture (for binding as Hi-Z in cull shader).
        /// The cull shader samples at the appropriate mip level.
        /// </summary>
        public RenderTexture Mip0Texture
        {
            get { return _mipTextures != null && _mipTextures.Length > 0 ? _mipTextures[0] : null; }
        }

        public int Width
        {
            get { return _width; }
        }

        public int Height
        {
            get { return _height; }
        }

        /// <summary>
        /// Combined mipmapped texture containing all mip levels, for compute shader sampling.
        /// </summary>
        public RenderTexture CombinedTexture
        {
            get { return _combinedTexture; }
        }

        private void CreateMipTextures(int width, int height)
        {
            _width = width;
            _height = height;
            _mipCount = 1 + (int)Mathf.Floor(Mathf.Log(Mathf.Max(width, height), 2));

            _mipTextures = new RenderTexture[_mipCount];

            for (int mip = 0; mip < _mipCount; mip++)
            {
                int mipW = Mathf.Max(1, width >> mip);
                int mipH = Mathf.Max(1, height >> mip);

                RenderTexture rt = new RenderTexture(mipW, mipH, 0, RenderTextureFormat.RFloat);
                rt.enableRandomWrite = true;
                rt.filterMode = FilterMode.Point;
                rt.wrapMode = TextureWrapMode.Clamp;
                rt.useMipMap = false;
                rt.autoGenerateMips = false;
                rt.name = $"HiZ_Mip{mip}";
                rt.Create();

                _mipTextures[mip] = rt;
            }

            // Combined mipmapped RT for sampling in occlusion cull compute shader.
            // Per-mip RTs are used during generation (avoids DX11 read-write hazard),
            // then copied into this combined RT for efficient mip-level access.
            _combinedTexture = new RenderTexture(_width, _height, 0, RenderTextureFormat.RFloat);
            _combinedTexture.useMipMap = true;
            _combinedTexture.autoGenerateMips = false;
            _combinedTexture.filterMode = FilterMode.Point;
            _combinedTexture.wrapMode = TextureWrapMode.Clamp;
            _combinedTexture.name = "HiZ_Combined";
            _combinedTexture.Create();
        }

        private void ReleaseMipTextures()
        {
            if (_mipTextures == null)
            {
                return;
            }

            for (int i = 0; i < _mipTextures.Length; i++)
            {
                if (_mipTextures[i] != null)
                {
                    _mipTextures[i].Release();
                    UnityEngine.Object.Destroy(_mipTextures[i]);
                    _mipTextures[i] = null;
                }
            }

            if (_combinedTexture != null)
            {
                _combinedTexture.Release();
                UnityEngine.Object.Destroy(_combinedTexture);
                _combinedTexture = null;
            }

            _mipTextures = null;
        }

        public void Dispose()
        {
            ReleaseMipTextures();
            _isValid = false;
        }
    }
}
