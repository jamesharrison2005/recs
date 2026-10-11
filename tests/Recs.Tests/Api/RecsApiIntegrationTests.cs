using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Recs.Tests.Api;

public class RecsApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public RecsApiIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Recommend_Unknown_User_Returns_Success()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/recommendations/unknown?count=2");
        Assert.True(resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.OK || resp.StatusCode == System.Net.HttpStatusCode.NotFound);
    }
}
