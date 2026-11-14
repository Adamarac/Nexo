using System.Net;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Ingestion;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Api;

[Collection(InfrastructureCollection.Name)]
public sealed class IngestionApiTests(InfrastructureFixture infrastructure) : ApiTestBase(infrastructure)
{
    [Fact]
    public async Task Files_ShowStatusAndCounts()
    {
        var files = await GetAsync<PagedResult<IngestionFileView>>("/api/v1/ingestion/files");

        files.Items.Should().HaveCount(2).And.OnlyContain(file => file.Status == "Processed");
        files.Items.Single(file => file.SourceKey == "cda_fi_202609").RowsRead.Should().Be(891);
    }

    [Fact]
    public async Task Status_CountsFilesBySituation()
    {
        var status = await GetAsync<IngestionStatus>("/api/v1/ingestion/status");

        status.FilesByStatus.Should().ContainKey("Processed").WhoseValue.Should().Be(2);
        status.LastProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Reprocess_WithoutTheAdminKey_IsForbidden()
    {
        var file = (await GetAsync<PagedResult<IngestionFileView>>("/api/v1/ingestion/files")).Items[0];

        var response = await Client.PostAsync(new Uri($"/api/v1/ingestion/files/{file.Id}/reprocess", UriKind.Relative), null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reprocess_WithTheAdminKey_IsAccepted()
    {
        var file = (await GetAsync<PagedResult<IngestionFileView>>("/api/v1/ingestion/files")).Items[0];
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ingestion/files/{file.Id}/reprocess");
        request.Headers.Add("X-Api-Key", ApiFactory.AdminKey);

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var reprocessed = await GetAsync<IngestionFileView>($"/api/v1/ingestion/files/{file.Id}");
        reprocessed.Status.Should().Be("Downloaded");
    }

    [Fact]
    public async Task InvalidRejectionReason_ReturnsBadRequest()
    {
        var response = await Client.GetAsync(new Uri("/api/v1/ingestion/files/1/rejections?reason=Qualquer", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
