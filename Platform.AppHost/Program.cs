var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume();
var enrollmentsDb = sql.AddDatabase("EnrollmentsDb");
var catalogDb = sql.AddDatabase("CatalogDb");

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume();
var reviewsDb = mongo.AddDatabase("ReviewsMongo");

var redis = builder.AddRedis("redis")
    .WithDataVolume();

var enrollments = builder.AddProject<Projects.Enrollments_Api>("enrollments-api")
    .WithReference(enrollmentsDb)
    .WaitFor(enrollmentsDb)
    .WithHttpHealthCheck("/health", endpointName: "http");

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithHttpHealthCheck("/health", endpointName: "http");

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(enrollments)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(redis)
    .WaitFor(enrollments)
    .WaitFor(catalog)
    .WithHttpHealthCheck("/health", endpointName: "http");

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(enrollments)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(aggregator)
    .WithHttpHealthCheck("/health", endpointName: "http");

builder.Build().Run();
