using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
        settings.ClusterConfigurator = cluster =>
            cluster.Subscribe(new DiagnosticsActivityEventSubscriber());
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>("reviews");
}
