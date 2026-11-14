using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Nexo.Application.Observability;

/// <summary>Traces e métricas do Nexo. Tudo aparece no Grafana com o prefixo "nexo.".</summary>
public static class NexoTelemetry
{
    public const string Name = "Nexo";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> FilesChecked = Meter.CreateCounter<long>("nexo.watcher.files_checked");

    public static readonly Counter<long> FilesDownloaded = Meter.CreateCounter<long>("nexo.watcher.files_downloaded");

    public static readonly Counter<long> RowsRead = Meter.CreateCounter<long>("nexo.processing.rows_read");

    public static readonly Counter<long> RowsRejected = Meter.CreateCounter<long>("nexo.processing.rows_rejected");

    public static readonly Counter<long> ProcessingFailures = Meter.CreateCounter<long>("nexo.processing.failures");

    public static readonly Histogram<double> ProcessingDuration = Meter.CreateHistogram<double>("nexo.processing.duration", unit: "s");

    public static readonly Counter<long> MessagesPublished = Meter.CreateCounter<long>("nexo.messaging.published");

    public static readonly Counter<long> MessagesConsumed = Meter.CreateCounter<long>("nexo.messaging.consumed");

    public static readonly Counter<long> MessagesRetried = Meter.CreateCounter<long>("nexo.messaging.retried");

    public static readonly Counter<long> MessagesDeadLettered = Meter.CreateCounter<long>("nexo.messaging.dead_lettered");

    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("nexo.cache.hits");

    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("nexo.cache.misses");

    private static long _queueDepth;

    static NexoTelemetry() =>
        Meter.CreateObservableGauge("nexo.messaging.queue_depth", () => Interlocked.Read(ref _queueDepth));

    public static void ReportQueueDepth(long depth) => Interlocked.Exchange(ref _queueDepth, depth);
}
