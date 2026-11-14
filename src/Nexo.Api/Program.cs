using Nexo.Api;
using Nexo.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddNexoObservability("nexo-api");
builder.AddNexoApi();

var app = builder.Build();

app.UseNexoApi();
app.Run();
