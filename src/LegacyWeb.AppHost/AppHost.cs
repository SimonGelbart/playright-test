var builder = DistributedApplication.CreateBuilder(args);

var legacyWeb = builder.AddDockerfile(
        "legacy-web",
        "../..")
    .WithHttpEndpoint(targetPort: 80, name: "http")
    .WithHttpHealthCheck("/", endpointName: "http");

builder.Build().Run();
