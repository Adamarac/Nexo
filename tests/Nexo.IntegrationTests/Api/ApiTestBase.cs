using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Api;

/// <summary>Carrega o cadastro e o mês de amostra num banco novo e sobe a API em cima dele.</summary>
public abstract class ApiTestBase(InfrastructureFixture infrastructure) : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly FakeCvmServer _cvm = new();
    private IHost _worker = null!;

    protected ApiFactory Api { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = await infrastructure.CreateDatabaseAsync();
        _worker = TestWorker.Build(infrastructure, connectionString, _cvm);

        _cvm.Publish(FakeCvmServer.RegistryFolder, "registro_fundo_classe.zip", FixtureArchives.RegistryZip());
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());
        var driver = new IngestionDriver(_worker.Services);
        await driver.CheckCvmAsync();
        await driver.ProcessPendingMessagesAsync();

        Api = CreateApi(infrastructure, connectionString);
        Client = Api.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Api.DisposeAsync();
        _worker.Dispose();
    }

    protected virtual ApiFactory CreateApi(InfrastructureFixture infrastructure, string connectionString) =>
        new(infrastructure, connectionString);

    protected async Task<T> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(new Uri(url, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected async Task<long> FundIdAsync(string search)
    {
        using var page = await GetAsync<JsonDocument>($"/api/v1/funds?search={Uri.EscapeDataString(search)}");
        return page.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64();
    }
}
