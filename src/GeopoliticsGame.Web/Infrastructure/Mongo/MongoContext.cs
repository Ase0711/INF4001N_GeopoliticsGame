using GeopoliticsGame.Core.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace GeopoliticsGame.Web.Infrastructure.Mongo;

public sealed class MongoContext
{
    public const string UsersCollection = "users";
    public const string GamesCollection = "games";
    public const string CountriesCollection = "countries";

    private static int _conventionsRegistered;

    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users => Database.GetCollection<User>(UsersCollection);
    public IMongoCollection<GameState> Games => Database.GetCollection<GameState>(GamesCollection);
    public IMongoCollection<CountryInfo> Countries => Database.GetCollection<CountryInfo>(CountriesCollection);

    public MongoContext(DatabaseOptions options)
    {
        RegisterConventions();

        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(8);
        settings.ApplicationName = "GeopoliticsGame";

        var client = new MongoClient(settings);
        Database = client.GetDatabase(options.DatabaseName);
    }


    public async Task PingAsync(CancellationToken ct = default) =>
        await Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);


    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await Users.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Name = "ux_username" }),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_email" })
        }, ct);

        await Games.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<GameState>(Builders<GameState>.IndexKeys
                    .Ascending(g => g.OwnerUserId)
                    .Descending(g => g.UpdatedAt),
                new CreateIndexOptions { Name = "ix_owner_updated" }),
            new CreateIndexModel<GameState>(Builders<GameState>.IndexKeys.Ascending(g => g.Status),
                new CreateIndexOptions { Name = "ix_status" })
        }, ct);
    }

    private static void RegisterConventions()
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 1) return;

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true),
            new IgnoreIfNullConvention(true)
        };
        ConventionRegistry.Register("GeopoliticsGame", pack, type =>
            type.Namespace?.StartsWith("GeopoliticsGame.Core", StringComparison.Ordinal) == true);
    }
}
