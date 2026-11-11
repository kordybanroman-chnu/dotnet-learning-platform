using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    private readonly MongoDbOptions _options;

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        _options = options.Value;
        var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
        if (!string.IsNullOrWhiteSpace(_options.ReplicaSet))
            settings.ReplicaSetName = _options.ReplicaSet;
        settings.ClusterConfigurator = cluster =>
            cluster.Subscribe(new DiagnosticsActivityEventSubscriber());
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(_options.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public bool SupportsTransactions => !string.IsNullOrWhiteSpace(_options.ReplicaSet);

    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>("reviews");
}
