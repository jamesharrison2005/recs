using Recs.Application.Common.Interfaces;
using Recs.Application.Services;
using Recs.Infrastructure;
using Recs.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddSingleton<Recs.ML.IRatingScorer>(sp => new Recs.ML.RatingScorer(builder.Configuration["ModelPath"]));
builder.Services.AddScoped<Recs.Application.Common.Interfaces.IRecommender, Recs.Application.Services.Recommender>();
builder.Services.AddScoped<IRecommendationProvider, Recs.ML.ColdStart.CollaborativeFilteringProvider>();
builder.Services.AddSingleton<Recs.Application.Common.Interfaces.IRecommendationProvider, Recs.ML.ColdStart.PopularityFallthroughProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapRecommendations();

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
