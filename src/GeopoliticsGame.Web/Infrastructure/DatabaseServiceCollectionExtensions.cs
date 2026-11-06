using GeopoliticsGame.Core.Abstractions;
using GeopoliticsGame.Web.Infrastructure.Memory;
#if !NO_MONGO
using GeopoliticsGame.Web.Infrastructure.Mongo;
#endif

namespace GeopoliticsGame.Web.Infrastructure;

public static class DatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Registers the data layer. With Provider "Auto" (the default) MongoDB is used when a
    /// connection string is set and the server answers a ping; otherwise the in-memory store
    /// is used (unless <see cref="DatabaseOptions.AllowMemoryFallback"/> is false).
    /// </summary>
    public static IServiceCollection AddGameDatabase(
        this IServiceCollection services, IConfiguration configuration, ILogger logger)
    {
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var provider = options.Provider.Trim().ToLowerInvariant();
        var wantsMongo = provider == "mongo" ||
                         (provider == "auto" && !string.IsNullOrWhiteSpace(options.ConnectionString));

        if (wantsMongo)
        {
#if NO_MONGO
            const string reason = "this build was compiled without the MongoDB driver (NoMongo=true)";
            if (!options.AllowMemoryFallback) throw new InvalidOperationException($"MongoDB requested but {reason}.");
            logger.LogWarning("MongoDB requested but {Reason}; using the in-memory store.", reason);
            return services.AddMemoryStore(new DatabaseInfo("In-memory", reason));
#else
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                throw new InvalidOperationException("Database:Provider is 'Mongo' but Database:ConnectionString is empty.");

            try
            {
                var context = new MongoContext(options);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                context.PingAsync(cts.Token).GetAwaiter().GetResult();
                context.EnsureIndexesAsync(cts.Token).GetAwaiter().GetResult();

                services.AddSingleton(context);
                services.AddSingleton<IUserRepository, MongoUserRepository>();
                services.AddSingleton<IGameRepository, MongoGameRepository>();
                services.AddSingleton<ICountryRepository, MongoCountryRepository>();
                services.AddSingleton(new DatabaseInfo("MongoDB", $"database '{options.DatabaseName}'"));
                logger.LogInformation("Connected to MongoDB database '{Database}'.", options.DatabaseName);
                return services;
            }
            catch (Exception ex) when (options.AllowMemoryFallback)
            {
                var reason = DescribeFailure(ex);
                logger.LogWarning(ex, "Could not connect to MongoDB ({Reason}); falling back to the in-memory store.", reason);
                return services.AddMemoryStore(new DatabaseInfo("In-memory", $"MongoDB connection failed: {reason}"));
            }
#endif
        }

        logger.LogWarning("No MongoDB connection string configured; using the in-memory store (data is lost on restart).");
        return services.AddMemoryStore(new DatabaseInfo("In-memory", "no connection string configured"));
    }

    /// <summary>A short, password-free explanation of why the connection failed (shown in the footer).</summary>
    private static string DescribeFailure(Exception ex)
    {
        var root = ex;
        while (root.InnerException is not null && root is not TimeoutException) root = root.InnerException;
        var all = ex.ToString();

        string hint;
        if (all.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase) ||
            all.Contains("bad auth", StringComparison.OrdinalIgnoreCase))
            hint = "wrong username or password (check Database Access in Atlas, and that <db_password> was replaced)";
        else if (ex is TimeoutException || all.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            hint = "timed out reaching the server (add your IP under Network Access in Atlas)";
        else if (all.Contains("DnsClient", StringComparison.OrdinalIgnoreCase) ||
                 all.Contains("SRV", StringComparison.Ordinal) ||
                 all.Contains("No such host", StringComparison.OrdinalIgnoreCase))
            hint = "couldn't look up the mongodb+srv address (try another network, or Atlas' standard connection string)";
        else if (all.Contains("connection string", StringComparison.OrdinalIgnoreCase))
            hint = "the connection string isn't valid (check for a missing password or special characters)";
        else
            hint = root.Message;

        // Never leak credentials into the page.
        hint = System.Text.RegularExpressions.Regex.Replace(hint, @"//[^/@\s]+@", "//****@");
        return hint.Length > 220 ? hint[..220] + "…" : hint;
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
