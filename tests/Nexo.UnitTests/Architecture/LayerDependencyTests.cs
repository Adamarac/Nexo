using NetArchTest.Rules;
using Nexo.Application.Ingestion;
using Nexo.Domain.Common;

namespace Nexo.UnitTests.Architecture;

/// <summary>Garante que as camadas continuem separadas: o domínio não conhece banco, fila nem HTTP.</summary>
public sealed class LayerDependencyTests
{
    private static readonly string[] InfrastructureLibraries =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "RabbitMQ.Client",
        "StackExchange.Redis",
    ];

    [Fact]
    public void Domain_DependsOnNothingElseInTheSolution()
    {
        var result = Types.InAssembly(typeof(Cnpj).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Nexo.Application", "Nexo.Infrastructure", "Nexo.Api", "Nexo.Worker", "FluentValidation", .. InfrastructureLibraries])
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Application_DoesNotKnowAboutInfrastructure()
    {
        var result = Types.InAssembly(typeof(CheckCvmForUpdates).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Nexo.Infrastructure", "Nexo.Api", "Nexo.Worker", "System.Net.Http", .. InfrastructureLibraries])
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Api_UsesInfrastructureOnlyThroughTheApplication()
    {
        var result = Types.InAssembly(typeof(Nexo.Api.Endpoints.FundEndpoints).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureLibraries)
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Worker_UsesInfrastructureOnlyThroughTheApplication()
    {
        var result = Types.InAssembly(typeof(Nexo.Worker.WatcherSchedule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureLibraries)
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }
}
