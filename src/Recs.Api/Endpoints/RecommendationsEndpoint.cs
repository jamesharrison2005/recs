using Recs.Application.Common.Interfaces;
using Recs.Application.Models;
using Recs.Domain.Entities;

namespace Recs.Api.Endpoints;

public static class RecommendationsEndpoint
{
    public static IEndpointRouteBuilder MapRecommendations(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/recommendations/{userId}", async (
            string userId,
            int? n,
            string? itemType,
            IPredictionService service,
            CancellationToken ct) =>
        {
            // 1. API validation
            if (string.IsNullOrWhiteSpace(userId))
                return Results.BadRequest(new { error = "userId is required." });

            var filters = new ItemFilter { ItemType = itemType };
            var result = await service.PredictAsync(userId, n ?? 10, filters);
            return Results.Ok(result);
        })
        .WithName("GetRecommendations")
        .WithOpenApi();

        return routes;
    }
}
