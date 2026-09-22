#if KNI_WEB
using System;
using System.Diagnostics;

// Temporary browser diagnostics: aggregate CPU timings, never log per button/frame.
internal static class WebMenuTiming
{
    internal enum Phase { Input, Draw, Borders, Lines }
    private static readonly double[] totals = new double[4];
    private static readonly int[] counts = new int[4];
    private static long reportStart = Stopwatch.GetTimestamp();
    private static int frames;

    internal readonly struct Sample : IDisposable
    {
        private readonly Phase phase;
        private readonly long start;
        internal Sample(Phase phase)
        {
            this.phase = phase;
            start = Stopwatch.GetTimestamp();
        }
        public void Dispose()
        {
            totals[(int)phase] += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            counts[(int)phase]++;
            if (phase != Phase.Draw) return;
            frames++;
            double seconds = Stopwatch.GetElapsedTime(reportStart).TotalSeconds;
            if (seconds < 2) return;
            bool open = RenderGuiSystem.Instance?.IsOverlayVisible == true;
            var camera = RenderingLibrary.SystemManagers.Default.Renderer.Camera;
            Console.WriteLine($"WEB MENU: open={open}, draw FPS={frames / seconds:F1}, CPU ms: input={Average(Phase.Input):F2}, draw={Average(Phase.Draw):F2}, borders={Average(Phase.Borders):F2}, lines={Average(Phase.Lines):F2}; hover={GumService.Default.Cursor.VisualOver?.Name ?? "none"}, camera={camera.ClientWidth}x{camera.ClientHeight}, zoom={camera.Zoom:F2}");
            Array.Clear(totals);
            Array.Clear(counts);
            frames = 0;
            reportStart = Stopwatch.GetTimestamp();
        }
    }
    private static double Average(Phase phase) => counts[(int)phase] == 0 ? 0 : totals[(int)phase] / counts[(int)phase];
}
#endif
