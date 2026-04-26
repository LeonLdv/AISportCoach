using AISportCoach.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: pgPassword)
    .WithImageRegistry("docker.io")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    .WithDataVolume("tenniscoach-pgdata")
    .WithHostPort(5432)
    .WithPgAdmin()
    .AddDatabase("tenniscoach");

builder.AddProject<Projects.AISportCoach_API>(ResourceNames.ApiService)
    .WithReference(postgres)
    .WaitFor(postgres);


builder.Build().Run();
