using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

public sealed class RatingBsonSerializer : SerializerBase<Rating>
{
    public override Rating Deserialize(
        BsonDeserializationContext context, BsonDeserializationArgs args)
        => new(context.Reader.ReadInt32());

    public override void Serialize(
        BsonSerializationContext context, BsonSerializationArgs args, Rating value)
        => context.Writer.WriteInt32(value.Value);
}

public static class MongoMappings
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        ConventionRegistry.Register(
            "ReviewsConventions",
            new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true)
            },
            _ => true);

        BsonSerializer.RegisterSerializer(new RatingBsonSerializer());

        BsonClassMap.RegisterClassMap<BaseEntity>(map => map.AutoMap());

        BsonClassMap.RegisterClassMap<Author>(map =>
        {
            map.AutoMap();
            map.MapCreator(a => new Author(a.UserId, a.DisplayName));
        });

        BsonClassMap.RegisterClassMap<Comment>(map =>
        {
            map.AutoMap();
            map.MapCreator(c => new Comment(c.Author, c.Text, c.CreatedAt));
        });

        BsonClassMap.RegisterClassMap<Review>(map =>
        {
            map.AutoMap();
            map.MapField("_photos").SetElementName("photos");
            map.MapField("_comments").SetElementName("comments");
        });
    }
}
