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

var modelPath = builder.Configuration["ModelPath"];
if (!string.IsNullOrWhiteSpace(modelPath) && !Path.IsPathRooted(modelPath))
{
    // ContentRootPath is the API project directory in dev (C:\dev\recs\src\Recs.Api).
    // The trained model lives at the repository root's data/ folder.
    var relativeToApi = Path.GetFullPath(modelPath, builder.Environment.ContentRootPath);
    modelPath = File.Exists(relativeToApi)
        ? relativeToApi
        : Path.GetFullPath(modelPath, Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
}
builder.Services.AddSingleton<IRatingScorer>(sp => new Recs.ML.RatingScorer(modelPath));

builder.Services.AddScoped<Recs.ML.ColdStart.PopularityFallthroughProvider>();
builder.Services.AddScoped<Recs.ML.ColdStart.ContentBasedProvider>();
builder.Services.AddScoped<IRecommendationProvider, Recs.ML.ColdStart.CompositeColdStartProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapRecommendations();

app.Run();

public partial class Program {}
