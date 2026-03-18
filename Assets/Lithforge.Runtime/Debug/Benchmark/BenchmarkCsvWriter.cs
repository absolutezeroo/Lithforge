using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Lithforge.Runtime.Debug.Benchmark
{
    /// <summary>
    /// Writes benchmark per-frame data to CSV. Extracts the CSV writing concern
    /// from the benchmark runner so it can be reused across different output strategies.
    /// </summary>
    public static class BenchmarkCsvWriter
    {
        public static void Write(BenchmarkResult result, string outputDir, string timestamp)
        {
            int count = result.TotalFrames;

            if (count == 0)
            {
                return;
            }

            StringBuilder csv = new(1024 * 64);

            // Header
            csv.Append("frame_index,frame_ms");

            for (int i = 0; i < FrameProfilerSections.SectionCount; i++)
            {
                csv.Append(',');
                csv.Append(FrameProfilerSections.SectionNames[i]);
                csv.Append("_ms");
            }

            csv.Append(",gen_completed,mesh_completed,lod_completed");
            csv.Append(",gpu_upload_bytes,gpu_upload_count,grow_events");
            csv.Append(",gc_gen0,gc_gen1,gc_gen2");
            csv.Append(",gen_scheduled,mesh_scheduled,lod_scheduled,invalidate_count");
            csv.Append(",mesh_complete_max_ms,mesh_complete_stalls");
            csv.Append(",gen_complete_max_ms,gen_complete_stalls");
            csv.Append(",sched_mesh_fill_ms,sched_mesh_filter_ms,sched_mesh_alloc_ms");
            csv.Append(",sched_mesh_schedule_ms,sched_mesh_flush_ms");
            csv.Append(",generated_set_size");
            csv.AppendLine();

            // Data rows
            for (int f = 0; f < count; f++)
            {
                csv.Append(f);
                csv.Append(',');
                csv.Append(result.FrameMs[f].ToString("F3", CultureInfo.InvariantCulture));

                for (int i = 0; i < FrameProfilerSections.SectionCount; i++)
                {
                    csv.Append(',');
                    csv.Append(result.SectionMs[i][f].ToString("F3", CultureInfo.InvariantCulture));
                }

                csv.Append(',');
                csv.Append(result.GenCompleted[f]);
                csv.Append(',');
                csv.Append(result.MeshCompleted[f]);
                csv.Append(',');
                csv.Append(result.LodCompleted[f]);
                csv.Append(',');
                csv.Append(result.GpuUploadBytes[f]);
                csv.Append(',');
                csv.Append(result.GpuUploadCount[f]);
                csv.Append(',');
                csv.Append(result.GrowEvents[f]);
                csv.Append(',');
                csv.Append(result.GcGen0[f]);
                csv.Append(',');
                csv.Append(result.GcGen1[f]);
                csv.Append(',');
                csv.Append(result.GcGen2[f]);
                csv.Append(',');
                csv.Append(result.GenScheduled[f]);
                csv.Append(',');
                csv.Append(result.MeshScheduled[f]);
                csv.Append(',');
                csv.Append(result.LodScheduled[f]);
                csv.Append(',');
                csv.Append(result.InvalidateCount[f]);
                csv.Append(',');
                csv.Append(result.MeshCompleteMaxMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(result.MeshCompleteStalls[f]);
                csv.Append(',');
                csv.Append(result.GenCompleteMaxMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(result.GenCompleteStalls[f]);
                csv.Append(',');
                csv.Append(result.SchedMeshFillMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(result.SchedMeshFilterMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(result.SchedMeshAllocMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(result.SchedMeshScheduleMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(result.SchedMeshFlushMs[f].ToString("F3", CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(result.GeneratedSetSize[f]);
                csv.AppendLine();
            }

            string safeName = result.ScenarioName.Replace(' ', '_');
            
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c, '_');
            }

            string path = Path.Combine(outputDir, safeName + "_" + timestamp + ".csv");
            
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(path, csv.ToString());
            UnityEngine.Debug.Log("[Benchmark] CSV written to: " + path);
        }
    }
}
