// Summarises a .nettrace recorded with the runtime's GC events at verbose level
// (benchmark.sh --allocs): what allocates, from where, and every garbage collection.
// AllocationTick events sample roughly one allocation per 100 KB, with its type and stack,
// so the figures are estimates; shares between sources are reliable.
// Usage: dotnet run --project tools/benchmark/allocs -- trace.nettrace [top]
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

string path = args[0];
int top = args.Length > 1 ? int.Parse(args[1]) : 20;
string etlx = TraceLog.CreateFromEventPipeDataFile(path);
using var log = new TraceLog(etlx);
var byType = new Dictionary<string, double>();
var bySite = new Dictionary<string, double>();
var byStack = new Dictionary<string, double>();
var collections = new List<(double At, int Gen, string Reason, double PauseMs, double PromotedMb)>();
double total = 0, suspendedAt = -1, gcAt = 0, promoted = 0;
int gcGen = 0;
string gcReason = "";
foreach (var ev in log.Events)
{
  switch (ev)
  {
    case GCAllocationTickTraceData tick:
    {
      double bytes = tick.AllocationAmount64;
      total += bytes;
      Add(byType, tick.TypeName ?? "?", bytes);
      string site = "?", chain = "";
      int depth = 0;
      for (var frame = tick.CallStack(); frame != null; frame = frame.Caller)
      {
        string method = frame.CodeAddress.FullMethodName;
        string module = frame.CodeAddress.ModuleName ?? "";
        if (string.IsNullOrEmpty(method)) continue;
        if (site == "?" && (module.Contains("UntitledGemGame") || module.Contains("JapeFramework")))
          site = Short(method);
        if (depth++ < 4) chain += (chain.Length > 0 ? " < " : "") + Short(method);
      }
      Add(bySite, site, bytes);
      Add(byStack, $"{tick.TypeName}: {chain}", bytes);
      break;
    }
    case GCSuspendEETraceData:
      if (suspendedAt < 0) suspendedAt = ev.TimeStampRelativeMSec;
      break;
    case GCStartTraceData start:
      gcAt = start.TimeStampRelativeMSec;
      gcGen = start.Depth;
      gcReason = start.Reason.ToString();
      break;
    case GCHeapStatsTraceData stats:
      promoted = (stats.TotalPromotedSize0 + stats.TotalPromotedSize1 + stats.TotalPromotedSize2) / 1e6;
      break;
    default:
      if (ev.EventName != null && ev.EventName.Contains("RestartEEStop") && suspendedAt >= 0)
      {
        collections.Add((gcAt, gcGen, gcReason, ev.TimeStampRelativeMSec - suspendedAt, promoted));
        suspendedAt = -1;
      }
      break;
  }
}

double seconds = log.SessionDuration.TotalSeconds;
Console.WriteLine($"Trace {seconds:F1} s: about {total / 1e6:F1} MB allocated ({total / 1e6 / seconds:F2} MB/s), " +
  $"{collections.Count} GCs, {collections.Sum(c => c.PauseMs):F0} ms paused");
Console.WriteLine("\nCollections (time s, generation, reason, pause, promoted):");
foreach (var c in collections)
  Console.WriteLine($"  {c.At / 1000,6:F2}  gen{c.Gen}  {c.Reason,-14} {c.PauseMs,6:F1} ms  {c.PromotedMb,7:F1} MB");
Print("By type", byType);
Print("By first game method on the stack", bySite);
Print("By type and top of stack", byStack);

void Add(Dictionary<string, double> map, string key, double value) => map[key] = map.GetValueOrDefault(key) + value;
void Print(string title, Dictionary<string, double> map)
{
  if (total == 0) return;
  Console.WriteLine($"\n{title}:");
  foreach (var (key, value) in map.OrderByDescending(kv => kv.Value).Take(top))
    Console.WriteLine($"  {value / total,6:P1}  {value / 1e6 / seconds,6:F2} MB/s  {key}");
}
static string Short(string method)
{
  method = method.Split('(')[0];
  var parts = method.Split('.');
  return parts.Length >= 2 ? string.Join('.', parts[^2..]) : method;
}
