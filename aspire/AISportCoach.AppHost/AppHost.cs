var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: pgPassword)
    .WithDataVolume("tenniscoach-pgdata")
    .WithHostPort(5432)
    .WithPgAdmin()
    .AddDatabase("tenniscoach");

builder.AddProject<Projects.AISportCoach_API>("api")
    .WithReference(postgres)
    .WaitFor(postgres);


builder.Build().Run();
