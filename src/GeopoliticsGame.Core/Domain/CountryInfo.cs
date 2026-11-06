namespace GeopoliticsGame.Core.Domain;

/// <summary>
/// A country from the reference catalogue (the "existing list of countries in the world").
/// The <see cref="Code"/> doubles as the document id in the database.
/// </summary>
public class CountryInfo
{
    public string Id { get; set; } = string.Empty;

    public string Code
    {
        get => Id;
        set => Id = value;
    }

    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute population (worldometers, approximate).</summary>
    public long Population { get; set; }

    public string Flag { get; set; } = string.Empty;

    public CountryInfo() { }

    public CountryInfo(string code, string name, long population, string flag)
    {
        Id = code;
        Name = name;
        Population = population;
        Flag = flag;
    }
}
