var builder = DistributedApplication.CreateBuilder(args);

var db = builder
    .AddPostgres("postgres", port: 5432)
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithInitBindMount("resources/init-db")
    .WithDataVolume("RAG-postgres-data")
    .WithPgAdmin()
    .AddDatabase("rag-db")
    .WithHealthCheck();

var phiModel = builder.AddOllama("phi", 11000, "phi3.5").WithContainerRuntimeArgs("--cpus=8", "--memory=30g");
var embeddingModel = builder.AddOllama("nomic", 12000, "nomic-embed-text");

builder.AddProject<Projects.RAG_Assistant_Service>("rag-assistant-service")
    .WithReference(db)
    .WithReference(phiModel)
    .WithReference(embeddingModel)
    .WaitFor(db)
    .WaitFor(phiModel)
    .WaitFor(embeddingModel);

builder.Build().Run();
