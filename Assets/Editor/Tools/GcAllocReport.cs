using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace LastGround.EditorTools.Tools
{
    /// <summary>
    /// Reads a profiler capture (from -lg-gc-capture) and lists the deepest main-thread scopes that allocate managed
    /// memory, with their marker path and bytes per frame. CLI:
    ///   -executeMethod LastGround.EditorTools.Tools.GcAllocReport.AnalyzeBatch -lgRaw /path/gc_Run.raw
    /// </summary>
    public static class GcAllocReport
    {
        public static void AnalyzeBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-lgRaw");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("-lgRaw <file> required");
            Debug.Log(Analyze(args[index + 1]));
            EditorApplication.Exit(0);
        }

        public static string Analyze(string rawPath)
        {
            if (!ProfilerDriver.LoadProfile(rawPath, false))
                return "[GcAllocReport] could not load " + rawPath;

            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;
            var totals = new Dictionary<string, (long bytes, int frames)>();
            int frames = 0;
            var children = new List<int>();

            for (int frame = first; frame <= last; frame++)
            {
                using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                           HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnGcMemory, false))
                {
                    if (view == null || !view.valid) continue;
                    frames++;
                    var seenThisFrame = new HashSet<string>();
                    Walk(view, view.GetRootItemID(), "", children, totals, seenThisFrame);
                }
            }

            var report = new StringBuilder();
            report.AppendLine($"[GcAllocReport] {rawPath}: {frames} frames");
            foreach (var entry in totals.OrderByDescending(e => e.Value.bytes).Take(25))
                report.AppendLine($"  {entry.Value.bytes / (double)Math.Max(1, frames),10:0.0} B/frame  in {entry.Value.frames,4} frames  {entry.Key}");
            return report.ToString();
        }

        /// <summary>Records scopes that allocate but whose children do not (the actual allocation sites).</summary>
        static void Walk(HierarchyFrameDataView view, int item, string path, List<int> scratch,
            Dictionary<string, (long bytes, int frames)> totals, HashSet<string> seen)
        {
            var kids = new List<int>();
            view.GetItemChildren(item, kids);
            long own = (long)view.GetItemColumnDataAsFloat(item, HierarchyFrameDataView.columnGcMemory);
            string name = item == view.GetRootItemID() ? "" : view.GetItemName(item);
            string here = string.IsNullOrEmpty(path) ? name : path + " > " + name;

            long childBytes = 0;
            foreach (int child in kids)
            {
                long bytes = (long)view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory);
                if (bytes <= 0) continue;
                childBytes += bytes;
                if (view.GetItemName(child) == "GC.Alloc") continue;
                Walk(view, child, here, scratch, totals, seen);
            }

            long direct = own - childBytes + SumGcAllocChildren(view, kids);
            if (direct > 0 && item != view.GetRootItemID())
            {
                totals.TryGetValue(here, out var t);
                t.bytes += direct;
                if (seen.Add(here)) t.frames++;
                totals[here] = t;
            }
        }

        static long SumGcAllocChildren(HierarchyFrameDataView view, List<int> kids)
        {
            long sum = 0;
            foreach (int child in kids)
                if (view.GetItemName(child) == "GC.Alloc")
                    sum += (long)view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory);
            return sum;
        }
    }
}
