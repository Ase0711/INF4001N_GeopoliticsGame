namespace GeopoliticsGame.Web.Infrastructure;

public sealed class DatabaseOptions
{

    public string Provider { get; set; } = "Auto";

 
    public string? ConnectionString { get; set; }

    public string DatabaseName { get; set; } = "geopolitics";

    public bool AllowMemoryFallback { get; set; } = true;
}

public sealed record DatabaseInfo(string Provider, string Detail)
{
    public bool IsPersistent => Provider == "MongoDB";
}
