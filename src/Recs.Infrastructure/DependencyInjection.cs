using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recs.Application.Common.Interfaces;
using Recs.Infrastructure.Persistence;
using Recs.Infrastructure.Persistence.Repositories;

namespace Recs.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionString = "Data Source=recs.db";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? DefaultConnectionString;

        services.AddDbContext<RecsDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<RecsDbContext>());

        return services;
    }
}
