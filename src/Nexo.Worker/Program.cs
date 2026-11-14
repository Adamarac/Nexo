using Nexo.Infrastructure.Observability;
using Nexo.Infrastructure.Persistence;
using Nexo.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddNexoObservability("nexo-worker");
builder.AddNexoWorker();

var host = builder.Build();

// "dotnet Nexo.Worker.dll migrate" só atualiza o banco e sai. O Docker Compose roda isso antes de tudo.
if (args.Contains("migrate"))
{
    await DatabaseMigrator.MigrateAsync(host.Services, CancellationToken.None);
    return;
}

await host.RunAsync();
