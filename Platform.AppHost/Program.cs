var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume();
var enrollmentsDb = sql.AddDatabase("EnrollmentsDb");
var catalogDb = sql.AddDatabase("CatalogDb");

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume();
var reviewsDb = mongo.AddDatabase("ReviewsMongo");

var enrollments = builder.AddProject<Projects.Enrollments_Api>("enrollments-api")
    .WithReference(enrollmentsDb)
    .WaitFor(enrollmentsDb);

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb);

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb);

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(enrollments)
    .WithReference(catalog)
    .WithReference(reviews)
    .WaitFor(enrollments)
    .WaitFor(catalog)
    .WaitFor(reviews);

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(enrollments)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(enrollments)
    .WaitFor(catalog)
    .WaitFor(reviews)
    .WaitFor(aggregator);

builder.Build().Run();
