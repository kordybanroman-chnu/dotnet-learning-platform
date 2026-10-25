var builder = DistributedApplication.CreateBuilder(args);

var enrollmentsDb = builder.AddConnectionString("EnrollmentsDb");
var catalogDb = builder.AddConnectionString("CatalogDb");
var reviewsMongo = builder.AddConnectionString("ReviewsMongo");

builder.AddProject<Projects.Enrollments_Api>("enrollments-api")
    .WithReference(enrollmentsDb);

builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb);

builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsMongo);

builder.Build().Run();
