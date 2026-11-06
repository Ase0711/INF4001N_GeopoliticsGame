namespace GeopoliticsGame.Web.Infrastructure;

/// <summary>Bound from the "Database" section of appsettings.json / user secrets / env vars.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"Auto" (MongoDB if configured and reachable, else memory), "Mongo" or "Memory".</summary>
    public string Provider { get; set; } = "Auto";

    /// <summary>MongoDB connection string, e.g. a MongoDB Atlas "mongodb+srv://…" URI.</summary>
    public string? ConnectionString { get; set; }

    public string DatabaseName { get; set; } = "geopolitics";

    /// <summary>If MongoDB is unreachable at start-up, fall back to the in-memory store instead of failing.</summary>
    public bool AllowMemoryFallback { get; set; } = true;
}

/// <summary>Which data store is actually in use (shown in the site footer).</summary>
public sealed record DatabaseInfo(string Provider, string Detail)
{
    public bool IsPersistent => Provider == "MongoDB";
}
