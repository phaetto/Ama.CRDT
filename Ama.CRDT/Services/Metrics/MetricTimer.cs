namespace Ama.CRDT.Services.Metrics;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public readonly record struct MetricTimer(Histogram<double> Histogram) : IDisposable
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public void Dispose()
    {
        Histogram.Record(stopwatch.Elapsed.TotalMilliseconds);
    }
}