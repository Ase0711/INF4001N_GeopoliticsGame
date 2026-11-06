using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Web.Infrastructure.Memory;
using GeopoliticsGame.Web.Infrastructure.Mongo;

namespace GeopoliticsGame.Web.Infrastructure;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddGameDatabase(
        this IServiceCollection services, IConfiguration configuration, ILogger logger)
    {
        var options = configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
        var provider = options.Provider?.Trim().ToLowerInvariant() ?? "auto";

        var useMongo = provider == "mongo" ||
                       (provider == "auto" && !string.IsNullOrWhiteSpace(options.ConnectionString));

        if (useMongo)
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException("MongoDB provider selected but connection string is empty.");
            }

            try
            {
                var context = new MongoContext(options);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                context.PingAsync(cts.Token).GetAwaiter().GetResult();
                context.EnsureIndexesAsync(cts.Token).GetAwaiter().GetResult();

                services.AddSingleton(context);
                services.AddSingleton<IUserRepository, MongoUserRepository>();
                services.AddSingleton<IGameRepository, MongoGameRepository>();
                services.AddSingleton<ICountryRepository, MongoCountryRepository>();
                services.AddSingleton(new DatabaseInfo("MongoDB", $"Database: {options.DatabaseName}"));

                logger.LogInformation("Successfully connected to MongoDB ({Database}).", options.DatabaseName);
                return services;
            }
            catch (Exception ex) when (options.AllowMemoryFallback)
            {
                logger.LogWarning(ex, "Failed to connect to MongoDB. Using in-memory fallback.");
                return services.AddMemoryStore(new DatabaseInfo("In-memory", "MongoDB unavailable - fallback active"));
            }
        }

        logger.LogInformation("Using in-memory repository store.");
        return services.AddMemoryStore(new DatabaseInfo("In-memory", "Configured for local memory store"));
    }

    private static IServiceCollection AddMemoryStore(this IServiceCollection services, DatabaseInfo info)
    {
        services.AddSingleton<IUserRepository, MemoryUserRepository>();
        services.AddSingleton<IGameRepository, MemoryGameRepository>();
        services.AddSingleton<ICountryRepository, MemoryCountryRepository>();
        services.AddSingleton(info);
        return services;
    }
}
