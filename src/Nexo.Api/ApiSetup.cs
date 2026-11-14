using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Nexo.Api.Endpoints;
using Nexo.Application.Queries.Validation;
using Nexo.Infrastructure;
using Serilog;

namespace Nexo.Api;

/// <summary>Monta a API. Fica separado do Program para os testes usarem exatamente a mesma montagem.</summary>
public static class ApiSetup
{
    public static WebApplicationBuilder AddNexoApi(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var options = builder.Configuration.GetSection(ApiOptions.Section).Get<ApiOptions>() ?? new ApiOptions();

        services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.Section));
        services.AddNexoDatabase(builder.Configuration).AddNexoQueries().AddNexoHealthChecks();
        services.AddValidatorsFromAssemblyContaining<FundSearchValidator>(ServiceLifetime.Singleton);

        services.AddProblemDetails();
        services.AddOpenApi();

        // Números sempre como número no JSON, nunca como texto; assim o contrato fica exato para o frontend.
        services.ConfigureHttpJsonOptions(json => json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
        services.AddCors(cors => cors.AddDefaultPolicy(policy =>
            policy.WithOrigins(options.AllowedOrigins).AllowAnyHeader().WithMethods("GET", "POST")));
        services.AddRateLimiter(limiter => ConfigureRateLimiter(limiter, options));

        return builder;
    }

    public static WebApplication UseNexoApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseSerilogRequestLogging();
        app.Use(AddSecurityHeaders);
        app.UseCors();
        app.UseRateLimiter();

        app.MapOpenApi();

        if (app.Environment.IsDevelopment())
            app.UseSwaggerUI(swagger => swagger.SwaggerEndpoint("/openapi/v1.json", "Nexo v1"));

        app.MapHealthChecks("/health");
        app.MapFundEndpoints()
            .MapPortfolioEndpoints()
            .MapAssetEndpoints()
            .MapAnalyticsEndpoints()
            .MapIngestionEndpoints();

        return app;
    }

    // Cada IP tem uma cota de requisições por janela; quem passa recebe 429 e sabe quando tentar de novo.
    private static void ConfigureRateLimiter(RateLimiterOptions limiter, ApiOptions options)
    {
        var window = TimeSpan.FromSeconds(options.WindowSeconds);

        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = options.PermitLimit, Window = window, QueueLimit = 0 }));

        limiter.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = options.WindowSeconds.ToString(CultureInfo.InvariantCulture);
            return ValueTask.CompletedTask;
        };
    }

    private static Task AddSecurityHeaders(HttpContext context, Func<Task> next)
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        return next();
    }
}
