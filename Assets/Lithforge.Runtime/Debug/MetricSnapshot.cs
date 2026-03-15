namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Value-type snapshot of all diagnostics. Committed once per frame by MetricsRegistry.
    /// Read by F3DebugOverlay and BenchmarkRunner on the main thread — no lock needed.
    /// Flat fields with switch-based indexed access to avoid array allocation in the struct.
    /// </summary>
    public struct MetricSnapshot
    {
        // --- Frame timing ---

        /// <summary>Wall-clock duration of the last completed frame in milliseconds.</summary>
        public float FrameMs;

        /// <summary>Exponentially smoothed frames-per-second, stable for display.</summary>
        public float FpsSmoothed;

        // --- FrameProfiler sections (15 values, indexed by FrameProfiler.* constants) ---
        // 0=PollGen, 1=PollMesh, 2=PollLOD, 3=LoadQueue, 4=Unload,
        // 5=SchedGen, 6=CrossLight, 7=Relight, 8=LODLevels, 9=SchedMesh,
        // 10=SchedLOD, 11=Render, 12=UpdateTotal, 13=Frame, 14=TickLoop

        /// <summary>Milliseconds spent in FrameProfiler section 0 (PollGen).</summary>
        public float SectionMs0;

        /// <summary>Milliseconds spent in FrameProfiler section 1 (PollMesh).</summary>
        public float SectionMs1;

        /// <summary>Milliseconds spent in FrameProfiler section 2 (PollLOD).</summary>
        public float SectionMs2;

        /// <summary>Milliseconds spent in FrameProfiler section 3 (LoadQueue).</summary>
        public float SectionMs3;

        /// <summary>Milliseconds spent in FrameProfiler section 4 (Unload).</summary>
        public float SectionMs4;

        /// <summary>Milliseconds spent in FrameProfiler section 5 (SchedGen).</summary>
        public float SectionMs5;

        /// <summary>Milliseconds spent in FrameProfiler section 6 (CrossLight).</summary>
        public float SectionMs6;

        /// <summary>Milliseconds spent in FrameProfiler section 7 (Relight).</summary>
        public float SectionMs7;

        /// <summary>Milliseconds spent in FrameProfiler section 8 (LODLevels).</summary>
        public float SectionMs8;

        /// <summary>Milliseconds spent in FrameProfiler section 9 (SchedMesh).</summary>
        public float SectionMs9;

        /// <summary>Milliseconds spent in FrameProfiler section 10 (SchedLOD).</summary>
        public float SectionMs10;

        /// <summary>Milliseconds spent in FrameProfiler section 11 (Render).</summary>
        public float SectionMs11;

        /// <summary>Milliseconds spent in FrameProfiler section 12 (UpdateTotal).</summary>
        public float SectionMs12;

        /// <summary>Milliseconds spent in FrameProfiler section 13 (Frame).</summary>
        public float SectionMs13;

        /// <summary>Milliseconds spent in FrameProfiler section 14 (TickLoop).</summary>
        public float SectionMs14;

        // --- Pipeline counters (from PipelineStats) ---

        /// <summary>Worldgen jobs dispatched to workers this frame.</summary>
        public int GenScheduled;

        /// <summary>Worldgen jobs that finished and were polled this frame.</summary>
        public int GenCompleted;

        /// <summary>LOD0 mesh jobs dispatched to workers this frame.</summary>
        public int MeshScheduled;

        /// <summary>LOD0 mesh jobs that finished and were polled this frame.</summary>
        public int MeshCompleted;

        /// <summary>LOD>0 mesh jobs dispatched to workers this frame.</summary>
        public int LodScheduled;

        /// <summary>LOD>0 mesh jobs that finished and were polled this frame.</summary>
        public int LodCompleted;

        /// <summary>Decoration passes (tree placement, etc.) executed this frame.</summary>
        public int DecorateCount;

        /// <summary>Total wall-clock time spent in decoration passes this frame.</summary>
        public float DecorateMs;

        /// <summary>Bytes uploaded to GPU vertex/index buffers this frame.</summary>
        public long GpuUploadBytes;

        /// <summary>Number of individual GPU upload operations this frame.</summary>
        public int GpuUploadCount;

        /// <summary>MegaMeshBuffer grow (reallocation) events triggered this frame.</summary>
        public int GrowEvents;

        /// <summary>Slots available in the MegaMeshBuffer free list at snapshot time.</summary>
        public int FreeListSize;

        /// <summary>Chunk mesh invalidations (remesh requests) issued this frame.</summary>
        public int InvalidateCount;

        /// <summary>Slowest mesh job Complete() call this frame in milliseconds.</summary>
        public float MeshCompleteMaxMs;

        /// <summary>Mesh Complete() calls that exceeded 1ms this frame, indicating main-thread stalls.</summary>
        public int MeshCompleteStalls;

        /// <summary>Slowest generation job Complete() call this frame in milliseconds.</summary>
        public float GenCompleteMaxMs;

        /// <summary>Generation Complete() calls that exceeded 1ms this frame, indicating main-thread stalls.</summary>
        public int GenCompleteStalls;

        // --- SchedMesh sub-timings ---

        /// <summary>Time spent filling the mesh candidate list in MeshScheduler.</summary>
        public float SchedMeshFillMs;

        /// <summary>Time spent filtering and sorting mesh candidates by priority.</summary>
        public float SchedMeshFilterMs;

        /// <summary>Time spent allocating NativeArrays for mesh output.</summary>
        public float SchedMeshAllocMs;

        /// <summary>Time spent calling Schedule() on mesh jobs.</summary>
        public float SchedMeshScheduleMs;

        /// <summary>Time spent flushing completed mesh data to GPU buffers.</summary>
        public float SchedMeshFlushMs;

        // --- Cumulative pipeline counters ---

        /// <summary>Lifetime count of worldgen jobs completed since launch.</summary>
        public int TotalGenerated;

        /// <summary>Lifetime count of LOD0 mesh jobs completed since launch.</summary>
        public int TotalMeshed;

        /// <summary>Lifetime count of LOD>0 mesh jobs completed since launch.</summary>
        public int TotalLod;

        /// <summary>Lifetime bytes uploaded to GPU since launch.</summary>
        public double TotalGpuUploadBytes;

        // --- GC ---

        /// <summary>GC generation-0 collections that occurred during the previous frame.</summary>
        public int GcGen0;

        /// <summary>GC generation-1 collections that occurred during the previous frame.</summary>
        public int GcGen1;

        /// <summary>GC generation-2 (full) collections that occurred during the previous frame.</summary>
        public int GcGen2;

        // --- Chunk state histogram (8 entries, indexed by (int)ChunkState) ---

        /// <summary>Chunks in Unloaded state (ChunkState ordinal 0).</summary>
        public int ChunkState0;

        /// <summary>Chunks in Loading state (ChunkState ordinal 1).</summary>
        public int ChunkState1;

        /// <summary>Chunks in Generating state (ChunkState ordinal 2).</summary>
        public int ChunkState2;

        /// <summary>Chunks in Decorating state (ChunkState ordinal 3).</summary>
        public int ChunkState3;

        /// <summary>Chunks in RelightPending state (ChunkState ordinal 4).</summary>
        public int ChunkState4;

        /// <summary>Chunks in Generated state (ChunkState ordinal 5).</summary>
        public int ChunkState5;

        /// <summary>Chunks in Meshing state (ChunkState ordinal 6).</summary>
        public int ChunkState6;

        /// <summary>Chunks in Ready state (ChunkState ordinal 7).</summary>
        public int ChunkState7;

        /// <summary>Chunks flagged for remeshing due to block edits or neighbor changes.</summary>
        public int NeedsRemeshCount;

        /// <summary>Chunks flagged for light recalculation after block edits.</summary>
        public int NeedsLightUpdateCount;

        // --- Pool ---

        /// <summary>NativeArray chunks in the pool ready for reuse.</summary>
        public int PoolAvailable;

        /// <summary>NativeArray chunks currently checked out to active ManagedChunks.</summary>
        public int PoolCheckedOut;

        /// <summary>Total NativeArray allocations (available + checked out) in the pool.</summary>
        public int PoolTotal;

        // --- VRAM (computed from MegaMeshBuffer capacities) ---

        /// <summary>Total estimated GPU memory in bytes across all render layers.</summary>
        public long VramTotalBytes;

        /// <summary>Opaque layer vertices currently written to the GPU buffer.</summary>
        public int OpaqueUsedVerts;

        /// <summary>Opaque layer vertex buffer capacity (grows on demand).</summary>
        public int OpaqueCapacityVerts;

        /// <summary>Cutout (alpha-test) layer vertices currently written to the GPU buffer.</summary>
        public int CutoutUsedVerts;

        /// <summary>Cutout layer vertex buffer capacity (grows on demand).</summary>
        public int CutoutCapacityVerts;

        /// <summary>Translucent (water) layer vertices currently written to the GPU buffer.</summary>
        public int TranslucentUsedVerts;

        /// <summary>Translucent layer vertex buffer capacity (grows on demand).</summary>
        public int TranslucentCapacityVerts;

        // --- World / Player ---

        /// <summary>Player world-space X position at snapshot time.</summary>
        public float PlayerX;

        /// <summary>Player world-space Y position at snapshot time.</summary>
        public float PlayerY;

        /// <summary>Player world-space Z position at snapshot time.</summary>
        public float PlayerZ;

        /// <summary>Chunk coordinate X the player is currently in.</summary>
        public int ChunkX;

        /// <summary>Chunk coordinate Y the player is currently in.</summary>
        public int ChunkY;

        /// <summary>Chunk coordinate Z the player is currently in.</summary>
        public int ChunkZ;

        /// <summary>Total chunks tracked by ChunkManager (all states).</summary>
        public int LoadedChunks;

        /// <summary>Active indirect-draw chunk renderers this frame.</summary>
        public int RendererCount;

        /// <summary>Whether the HiZ occlusion culling pass is active.</summary>
        public bool OcclusionCullingActive;

        /// <summary>Whether the player is currently in fly mode.</summary>
        public bool IsFlying;

        /// <summary>Whether noclip (no collision) is enabled.</summary>
        public bool IsNoclip;

        /// <summary>Current fly-mode speed multiplier.</summary>
        public float FlySpeed;

        // --- Tick ---

        /// <summary>Fixed-rate ticks executed during this frame's accumulator drain.</summary>
        public int TicksThisFrame;

        // --- Queues ---

        /// <summary>Chunks waiting in the generation scheduler queue.</summary>
        public int PendingGenCount;

        /// <summary>Chunks waiting in the LOD0 mesh scheduler queue.</summary>
        public int PendingMeshCount;

        /// <summary>Chunks waiting in the LOD>0 mesh scheduler queue.</summary>
        public int PendingLodMeshCount;

        // --- Index sizes ---

        /// <summary>Number of chunks in the Generated state set, eligible for meshing.</summary>
        public int GeneratedSetSize;

        /// <summary>
        /// Retrieves a FrameProfiler section timing by index without array allocation.
        /// </summary>
        public float GetSectionMs(int index)
        {
            switch (index)
            {
                case 0: return SectionMs0;
                case 1: return SectionMs1;
                case 2: return SectionMs2;
                case 3: return SectionMs3;
                case 4: return SectionMs4;
                case 5: return SectionMs5;
                case 6: return SectionMs6;
                case 7: return SectionMs7;
                case 8: return SectionMs8;
                case 9: return SectionMs9;
                case 10: return SectionMs10;
                case 11: return SectionMs11;
                case 12: return SectionMs12;
                case 13: return SectionMs13;
                case 14: return SectionMs14;
                default: return 0f;
            }
        }

        /// <summary>
        /// Retrieves chunk state histogram count by index without array allocation.
        /// </summary>
        public int GetChunkState(int index)
        {
            switch (index)
            {
                case 0: return ChunkState0;
                case 1: return ChunkState1;
                case 2: return ChunkState2;
                case 3: return ChunkState3;
                case 4: return ChunkState4;
                case 5: return ChunkState5;
                case 6: return ChunkState6;
                case 7: return ChunkState7;
                default: return 0;
            }
        }
    }
}
