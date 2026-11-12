using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recs.Infrastructure;
using Recs.Infrastructure.Persistence;
using Recs.ML.Training;

// Maps the documented command line flags onto configuration keys.
var switchMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["--model-path"] = "ModelPath",
    ["--db"] = "ConnectionStrings:DefaultConnection"
};

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args, switchMappings)
    .Build();

var modelPath = configuration["ModelPath"];
if (string.IsNullOrWhiteSpace(modelPath))
{
    Console.Error.WriteLine("Model path is required. Set ModelPath in appsettings.json or pass --model-path <path>.");
    return 1;
}

if (!Path.IsPathRooted(modelPath))
    modelPath = Path.GetFullPath(modelPath);

var modelDirectory = Path.GetDirectoryName(modelPath);
if (!string.IsNullOrEmpty(modelDirectory))
    Directory.CreateDirectory(modelDirectory);

var services = new ServiceCollection();
services.AddInfrastructure(configuration);

await using var provider = services.BuildServiceProvider();

await using var scope = provider.CreateAsyncScope();
var context = scope.ServiceProvider.GetRequiredService<RecsDbContext>();

var ratingsCount = await context.Ratings.CountAsync();
if (ratingsCount == 0)
{
    Console.Error.WriteLine("No ratings found in the database; nothing to train on.");
    PrintConnectionHint(context);
    return 1;
}

Console.WriteLine($"Database: {ResolveDatabaseFile(context)}");
Console.WriteLine($"Ratings:  {ratingsCount:N0}");
Console.WriteLine($"Model:    {modelPath}");
Console.WriteLine("Training matrix factorization (this can take a couple of minutes)...");

try
{
    var pipeline = new Pipeline(context, modelPath);
    var (model, _, testSet) = pipeline.Train();

    Console.WriteLine($"Model saved to {modelPath}");

    var evaluator = new Evaluator(model, testSet);
    Console.WriteLine();
    Console.WriteLine("=== Evaluation on held-out test set (20%) ===");
    Console.WriteLine($"RMSE: {evaluator.Rmse:F4}");
    Console.WriteLine($"R^2:  {evaluator.R2:F4}");
    Console.WriteLine($"MAE:  {evaluator.Mae:F4}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Training failed: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

static void PrintConnectionHint(RecsDbContext context)
{
    Console.Error.WriteLine($"Connection: {context.Database.GetConnectionString()}");
    Console.Error.WriteLine("A relative connection string is resolved against the process working directory.");
    Console.Error.WriteLine("Run from the repo root, or pass --db \"Data Source=<absolute path>\".");
}

static string ResolveDatabaseFile(RecsDbContext context)
{
    if (context.Database.GetDbConnection() is SqliteConnection connection &&
        !string.IsNullOrWhiteSpace(connection.DataSource))
    {
        return Path.GetFullPath(connection.DataSource);
    }

    return context.Database.GetConnectionString() ?? "(unset)";
}
