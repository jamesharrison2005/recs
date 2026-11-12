using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Recs.Infrastructure.Persistence;
using Recs.Tests.Infrastructure;
using Xunit;

namespace Recs.Tests.Api;

/// <summary>
/// Exercises the real HTTP pipeline against a seeded in-memory SQLite database.
/// The trained model is deliberately disabled (nonexistent <c>ModelPath</c>) so scoring
/// falls back to the deterministic popularity order: item-a &gt; item-c &gt; item-b &gt; item-d.
/// </summary>
public class RecsApiIntegrationTests
{
    private const string PopularityOrder = "item-a,item-c,item-b,item-d";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Recommend_Unknown_User_Returns_Top_Popular_Items()
    {
        using var db = new SeededApiTestDatabase();
        using var factory = CreateFactory(db);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/recommendations/unknown-user?n=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await ReadItemIdsAsync(response);
        Assert.Equal(2, ids.Count);
        Assert.Equal(PopularityOrder.Split(',').Take(2), ids);
    }

    [Fact]
    public async Task Recommend_Known_User_Excludes_Rated_Items()
    {
        using var db = new SeededApiTestDatabase();
        using var factory = CreateFactory(db);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/recommendations/user-1?n=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await ReadItemIdsAsync(response);
        // user-1 rated item-a and item-b; item-e is unrated so the popularity fallback never emits it.
        Assert.Equal(new[] { "item-c", "item-d" }, ids);
    }

    [Fact]
    public async Task Recommend_Honours_N_Parameter()
    {
        using var db = new SeededApiTestDatabase();
        using var factory = CreateFactory(db);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/recommendations/unknown-user?n=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await ReadItemIdsAsync(response);
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task Recommend_Empty_UserId_Returns_BadRequest()
    {
        using var db = new SeededApiTestDatabase();
        using var factory = CreateFactory(db);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/recommendations/%20?n=2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(SeededApiTestDatabase db) =>
        new TestWebApplicationFactory(db);

    private static async Task<IReadOnlyList<string>> ReadItemIdsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var ids = new List<string>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("id", out var id))
            {
                ids.Add(id.GetString()!);
            }
        }
        return ids;
    }

    private sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SeededApiTestDatabase _db;

        public TestWebApplicationFactory(SeededApiTestDatabase db) => _db = db;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Disable the trained model so popularity order is deterministic, and point the
            // connection string at a path that is never opened.
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=unused-integration-tests.db");
            builder.UseSetting("ModelPath", "recs-model-does-not-exist.zip");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<RecsDbContext>>();
                services.RemoveAll<RecsDbContext>();

                var options = _db.CreateOptions();
                services.AddSingleton(options);
                services.AddScoped(sp => new RecsDbContext(sp.GetRequiredService<DbContextOptions<RecsDbContext>>()));
            });
        }
    }
}
