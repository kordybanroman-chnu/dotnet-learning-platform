namespace Reviews.Infrastructure.Persistence;

public sealed class MongoDbOptions
{
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "";
}
