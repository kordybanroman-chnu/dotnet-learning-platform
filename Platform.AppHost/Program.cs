var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume();
var enrollmentsDb = sql.AddDatabase("EnrollmentsDb");
var catalogDb = sql.AddDatabase("CatalogDb");

var mongoUser = builder.AddParameter("mongo-user");
var mongoPassword = builder.AddParameter("mongo-password", secret: true);

var mongo = builder.AddMongoDB("mongo", null, mongoUser, mongoPassword)
    .WithDataVolume()
    .WithEndpoint("tcp", endpoint =>
    {
        endpoint.Port = 27017;
        endpoint.IsProxied = false;
    })
    .WithEntrypoint("/bin/bash")
    .WithArgs("-c", """
        KEY=/data/db/keyfile
        if [ ! -f "$KEY" ]; then
          head -c 48 /dev/urandom | base64 > "$KEY"
        fi
        chmod 400 "$KEY"
        chown mongodb:mongodb "$KEY"
        exec docker-entrypoint.sh mongod --replSet rs0 --keyFile "$KEY" --bind_ip_all
        """);
var reviewsDb = mongo.AddDatabase("ReviewsMongo");

var mongoInit = builder.AddContainer("mongo-init", "mongo:8.0")
    .WithEntrypoint("/bin/bash")
    .WithArgs("-c", """
        HOST_PORT="${MONGO_ENDPOINT##*:}"
        URI="$ConnectionStrings__mongo&directConnection=true"
        until mongosh "$URI" --quiet --eval 'db.runCommand({ ping: 1 }).ok' >/dev/null 2>&1; do sleep 1; done
        mongosh "$URI" --quiet --eval "try { rs.status() } catch (e) { rs.initiate({ _id: 'rs0', members: [{ _id: 0, host: 'localhost:$HOST_PORT' }] }) }"
        until mongosh "$URI" --quiet --eval 'db.hello().isWritablePrimary' | grep -q true; do sleep 1; done
        """)
    .WithEnvironment("MONGO_ENDPOINT", mongo.GetEndpoint("tcp"))
    .WithReference(mongo)
    .WaitForStart(mongo);

var redis = builder.AddRedis("redis")
    .WithDataVolume();

var rabbit = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin();

var enrollments = builder.AddProject<Projects.Enrollments_Api>("enrollments-api")
    .WithReference(enrollmentsDb)
    .WithReference(rabbit)
    .WaitFor(enrollmentsDb)
    .WaitFor(rabbit)
    .WithHttpHealthCheck("/health", endpointName: "http");

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WithReference(rabbit)
    .WaitFor(rabbit)
    .WithHttpHealthCheck("/health", endpointName: "http");

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb)
    .WithReference(rabbit)
    .WithEnvironment("MongoDb__ReplicaSet", "rs0")
    .WaitFor(reviewsDb)
    .WaitForCompletion(mongoInit)
    .WaitFor(rabbit)
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
