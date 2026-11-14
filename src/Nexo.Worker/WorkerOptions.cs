namespace Nexo.Worker;

/// <summary>Liga e desliga as partes do worker. Útil para escalar o processamento separado da coleta.</summary>
public sealed class WorkerOptions
{
    public const string Section = "Worker";

    public bool EnableProcessing { get; set; } = true;
}
