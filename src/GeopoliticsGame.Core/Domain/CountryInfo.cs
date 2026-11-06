namespace GeopoliticsGame.Core.Domain;


public class CountryInfo
{
    public string Id { get; set; } = string.Empty;

    public string Code
    {
        get => Id;
        set => Id = value;
    }

    public string Name { get; set; } = string.Empty;

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
