var builder = DistributedApplication.CreateBuilder(args);

var enrollmentsDb = builder.AddConnectionString("EnrollmentsDb");

builder.AddProject<Projects.Enrollments_Api>("enrollments-api")
    .WithReference(enrollmentsDb);

builder.Build().Run();
