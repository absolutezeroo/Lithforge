using System;
using System.Collections.Generic;
using Lithforge.Meshing;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lithforge.Runtime.Rendering
{
    /// <summary>
    /// Owns a single large Mesh containing the merged geometry for one render layer
    /// (opaque, cutout, or translucent). Chunks are allocated fixed-size "pages" within
    /// the vertex and index buffers. Pages need not be contiguous — vertices and indices
    /// are written page-by-page with index remapping. Freed pages are zeroed to produce
    /// degenerate triangles.
    /// Owner: ChunkMeshStore. Lifetime: application session.
    /// </summary>
    public sealed class MegaMeshBuffer : IDisposable
    {
        /// <summary>Vertices per page. Each chunk allocation is rounded up to this.</summary>
        private const int _vertexPageSize = 4096;

        /// <summary>Indices per page. Each chunk allocation is rounded up to this.</summary>
        private const int _indexPageSize = 6144;

        private readonly Mesh _mesh;
        private readonly int _totalVertexCapacity;
        private readonly int _totalIndexCapacity;

        // Free-list of page indices, kept sorted ascending for low-page-first allocation
        private readonly List<int> _freeVertexPages;
        private readonly List<int> _freeIndexPages;
        private readonly int _indexPageCount;
        private readonly int _vertexPageCount;

        // Per-page usage tracking for high-water mark recomputation
        private readonly bool[] _indexPageInUse;

        // Page-sized staging buffers — one page written at a time, no allocation cap needed
        private readonly MeshVertex[] _vertexStaging;
        private readonly int[] _indexStaging;

        // Pre-allocated zero buffer for clearing freed index pages
        private readonly int[] _zeroIndices;

        // Track the high-water mark for the submesh descriptor
        private int _maxUsedIndexOffset;
        private bool _subMeshDirty;

        public Mesh Mesh
        {
            get { return _mesh; }
        }

        public bool HasGeometry
        {
            get { return _maxUsedIndexOffset > 0; }
        }

        /// <summary>
        /// Creates a MegaMeshBuffer with the given page counts.
        /// </summary>
        /// <param name="bufferName">Name for the Mesh object (for debugging).</param>
        /// <param name="vertexPages">Number of vertex pages to pre-allocate.</param>
        /// <param name="indexPages">Number of index pages to pre-allocate.</param>
        public MegaMeshBuffer(string bufferName, int vertexPages, int indexPages)
        {
            _indexPageCount = indexPages;
            _vertexPageCount = vertexPages;
            _totalVertexCapacity = vertexPages * _vertexPageSize;
            _totalIndexCapacity = indexPages * _indexPageSize;

            _mesh = new Mesh
            {
                name = bufferName,
            };
            _mesh.MarkDynamic();

            // Pre-allocate vertex and index buffers on the GPU
            _mesh.SetVertexBufferParams(_totalVertexCapacity, MeshVertex.VertexAttributes);
            _mesh.SetIndexBufferParams(_totalIndexCapacity, IndexFormat.UInt32);

            // Initialize with a single empty submesh
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0,
                new SubMeshDescriptor(0, 0, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            // Set large bounds so the mesh is never frustum-culled by Unity
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

            // Build free-lists sorted ascending (0, 1, 2, ...)
            _freeVertexPages = new List<int>(vertexPages);
            _freeIndexPages = new List<int>(indexPages);

            for (int i = 0; i < vertexPages; i++)
            {
                _freeVertexPages.Add(i);
            }

            for (int i = 0; i < indexPages; i++)
            {
                _freeIndexPages.Add(i);
            }

            // Per-page usage tracking for high-water mark recomputation
            _indexPageInUse = new bool[indexPages];

            // Page-sized staging buffers — WriteSlot processes one page at a time
            _vertexStaging = new MeshVertex[_vertexPageSize];
            _indexStaging = new int[_indexPageSize];

            // Zero buffer for clearing freed index pages
            _zeroIndices = new int[_indexPageSize];
        }

        /// <summary>
        /// Allocates pages for a chunk with the given vertex and index counts.
        /// Pages need not be contiguous — allocation always succeeds if enough
        /// total pages are free. Returns MegaMeshSlot.Invalid if capacity is exceeded.
        /// </summary>
        public MegaMeshSlot Allocate(int vertexCount, int indexCount)
        {
            if (vertexCount <= 0 || indexCount <= 0)
            {
                return MegaMeshSlot.Invalid;
            }

            int vertexPagesNeeded = (vertexCount + _vertexPageSize - 1) / _vertexPageSize;
            int indexPagesNeeded = (indexCount + _indexPageSize - 1) / _indexPageSize;

            if (_freeVertexPages.Count < vertexPagesNeeded || _freeIndexPages.Count < indexPagesNeeded)
            {
                UnityEngine.Debug.LogWarning(
                    $"[MegaMeshBuffer] {_mesh.name}: insufficient pages " +
                    $"(need {vertexPagesNeeded}v/{indexPagesNeeded}i, " +
                    $"have {_freeVertexPages.Count}v/{_freeIndexPages.Count}i)");
                return MegaMeshSlot.Invalid;
            }

            // Pop the lowest available pages (front of the sorted list keeps high-water low)
            int[] vertexPages = PopPages(_freeVertexPages, vertexPagesNeeded);
            int[] indexPages = PopPages(_freeIndexPages, indexPagesNeeded);

            // Mark index pages as in-use for high-water tracking
            for (int i = 0; i < indexPagesNeeded; i++)
            {
                _indexPageInUse[indexPages[i]] = true;
            }

            MegaMeshSlot slot = new MegaMeshSlot
            {
                VertexPages = vertexPages,
                IndexPages = indexPages,
                VertexCount = vertexCount,
                IndexCount = indexCount,
            };

            return slot;
        }

        /// <summary>
        /// Frees the pages occupied by a slot and zeros the index data
        /// so the GPU renders degenerate triangles for that region.
        /// Recomputes the high-water mark if a freed page was at the frontier.
        /// </summary>
        public void Free(MegaMeshSlot slot)
        {
            if (!slot.IsValid)
            {
                return;
            }

            // Zero out each index page to produce degenerate triangles
            int highestFreedPage = 0;

            for (int p = 0; p < slot.IndexPages.Length; p++)
            {
                int page = slot.IndexPages[p];
                int pageOffset = page * _indexPageSize;
                _mesh.SetIndexBufferData(_zeroIndices, 0, pageOffset, _indexPageSize,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                _indexPageInUse[page] = false;

                if (page > highestFreedPage)
                {
                    highestFreedPage = page;
                }
            }

            // Return vertex pages to free-list (sorted insertion)
            for (int p = 0; p < slot.VertexPages.Length; p++)
            {
                InsertSorted(_freeVertexPages, slot.VertexPages[p]);
            }

            // Return index pages to free-list (sorted insertion)
            for (int p = 0; p < slot.IndexPages.Length; p++)
            {
                InsertSorted(_freeIndexPages, slot.IndexPages[p]);
            }

            // Recompute high-water mark if a freed page was at or near the frontier
            int freedEnd = (highestFreedPage + 1) * _indexPageSize;

            if (freedEnd >= _maxUsedIndexOffset)
            {
                RecomputeHighWaterMark();
            }

            _subMeshDirty = true;
        }

        /// <summary>
        /// Writes chunk mesh data into the allocated slot. Transforms vertices to world-space
        /// using the given chunk coordinate and patches indices to reference absolute vertex
        /// positions across potentially non-contiguous pages.
        /// Processes one page at a time to avoid large staging buffers.
        /// </summary>
        public void WriteSlot(
            MegaMeshSlot slot,
            int3 chunkCoord,
            NativeList<MeshVertex> vertices,
            NativeList<int> indices)
        {
            if (!slot.IsValid || vertices.Length == 0)
            {
                return;
            }

            float3 worldOffset = new float3(
                chunkCoord.x * 32,
                chunkCoord.y * 32,
                chunkCoord.z * 32);

            int vertCount = vertices.Length;
            int idxCount = indices.Length;

            // Write vertices one page at a time
            int vertsWritten = 0;

            for (int p = 0; p < slot.VertexPages.Length && vertsWritten < vertCount; p++)
            {
                int dstOffset = slot.VertexPages[p] * _vertexPageSize;
                int remaining = vertCount - vertsWritten;
                int count = remaining < _vertexPageSize ? remaining : _vertexPageSize;

                // Transform this page's vertices to world-space
                for (int i = 0; i < count; i++)
                {
                    MeshVertex v = vertices[vertsWritten + i];
                    v.Position = v.Position + worldOffset;
                    _vertexStaging[i] = v;
                }

                _mesh.SetVertexBufferData(_vertexStaging, 0, dstOffset, count, 0,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                vertsWritten += count;
            }

            // Write indices one page at a time with vertex remapping
            int idxWritten = 0;
            int highestIndexPage = 0;

            for (int p = 0; p < slot.IndexPages.Length; p++)
            {
                int page = slot.IndexPages[p];
                int dstOffset = page * _indexPageSize;
                int remaining = idxCount - idxWritten;
                int writeCount = remaining < _indexPageSize ? remaining : _indexPageSize;

                // Remap this page's indices: local vertex → absolute GPU vertex
                for (int i = 0; i < writeCount; i++)
                {
                    int localVert = indices[idxWritten + i];
                    int vPage = localVert / _vertexPageSize;
                    int vOffset = localVert % _vertexPageSize;
                    _indexStaging[i] = slot.VertexPages[vPage] * _vertexPageSize + vOffset;
                }

                if (writeCount > 0)
                {
                    _mesh.SetIndexBufferData(_indexStaging, 0, dstOffset, writeCount,
                        MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                }

                // Zero unused portion of this page
                int clearCount = _indexPageSize - writeCount;

                if (clearCount > 0)
                {
                    _mesh.SetIndexBufferData(_zeroIndices, 0, dstOffset + writeCount, clearCount,
                        MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                }

                idxWritten += writeCount;

                if (page > highestIndexPage)
                {
                    highestIndexPage = page;
                }
            }

            // Update high-water mark for submesh descriptor
            int newHighWater = (highestIndexPage + 1) * _indexPageSize;

            if (newHighWater > _maxUsedIndexOffset)
            {
                _maxUsedIndexOffset = newHighWater;
                _subMeshDirty = true;
            }
        }

        /// <summary>
        /// Updates the submesh descriptor if dirty. Should be called before rendering.
        /// </summary>
        public void FlushSubMesh()
        {
            if (!_subMeshDirty)
            {
                return;
            }

            _mesh.SetSubMesh(0,
                new SubMeshDescriptor(0, _maxUsedIndexOffset, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            _subMeshDirty = false;
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                UnityEngine.Object.Destroy(_mesh);
            }
        }

        /// <summary>
        /// Pops N pages from the front of the sorted free-list (lowest page numbers first).
        /// </summary>
        private static int[] PopPages(List<int> freeList, int count)
        {
            int[] pages = new int[count];

            for (int i = 0; i < count; i++)
            {
                pages[i] = freeList[i];
            }

            freeList.RemoveRange(0, count);
            return pages;
        }

        /// <summary>
        /// Inserts a page index into the sorted free-list at the correct position.
        /// </summary>
        private static void InsertSorted(List<int> freeList, int page)
        {
            int index = freeList.BinarySearch(page);

            if (index < 0)
            {
                index = ~index;
            }

            freeList.Insert(index, page);
        }

        /// <summary>
        /// Scans the index page usage array backwards to find the new high-water mark.
        /// Called when a freed slot was at or near the current frontier.
        /// </summary>
        private void RecomputeHighWaterMark()
        {
            int highPage = -1;

            for (int i = _indexPageCount - 1; i >= 0; i--)
            {
                if (_indexPageInUse[i])
                {
                    highPage = i;
                    break;
                }
            }

            if (highPage < 0)
            {
                _maxUsedIndexOffset = 0;
            }
            else
            {
                _maxUsedIndexOffset = (highPage + 1) * _indexPageSize;
            }
        }
    }
}
