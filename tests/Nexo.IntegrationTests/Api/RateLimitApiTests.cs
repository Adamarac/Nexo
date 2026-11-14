using System.Net;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Api;

[Collection(InfrastructureCollection.Name)]
public sealed class RateLimitApiTests(InfrastructureFixture infrastructure) : ApiTestBase(infrastructure)
{
    protected override ApiFactory CreateApi(InfrastructureFixture infrastructure, string connectionString) =>
        new(infrastructure, connectionString, permitLimit: 2);

    [Fact]
    public async Task TooManyRequests_ReturnTooManyRequestsWithRetryAfter()
    {
        var uri = new Uri("/api/v1/funds", UriKind.Relative);
        await Client.GetAsync(uri);
        await Client.GetAsync(uri);

        var response = await Client.GetAsync(uri);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
    }
}
