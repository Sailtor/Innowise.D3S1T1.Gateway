using Gateway.AppHost;
using Gateway.Application;
using Gateway.Infrastructure;
using Gateway.Presentation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

// Bootstrap logger: without it, anything that fails before the host is built - a missing
// connection string, a bad schema - is written nowhere and the container exits silently.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddObservability();

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddPresentation(builder.Configuration);

    var app = builder.Build();

    // First in the pipeline so its one-line-per-request summary times everything below it.
    app.UseSerilogRequestLogging();

    // Deliberately no UseHttpsRedirection(). In a container there is no dev certificate and, in
    // compose, no HTTPS port - so it would either no-op with a warning or 307 clients to a port
    // nothing listens on. TLS belongs at the edge, not in this process.
    app.UseCors();

    // Readiness: runs the metrics-db check. This is what the image's HEALTHCHECK and compose's
    // service_healthy gate point at.
    app.MapHealthChecks("/health");

    // Liveness: no checks at all, so it answers "the process is up and serving" even while the
    // database is unreachable. Separate from /health so a restart-on-failure probe cannot
    // restart-loop this service over a schema it does not own.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

    if (app.Environment.IsDevelopment())
    {
        // Nothing else is mapped at the root, and a 404 there reads as "the service is down" when
        // it is perfectly fine. Only useful where Nitro is actually served, hence the guard.
        app.MapGet("/", () => Results.Redirect("/graphql"));
    }

    // Nitro (the built-in IDE) is this service's API documentation, but it has no business being
    // served in production. Introspection is governed separately by GraphQL:AllowIntrospection.
    app.MapGraphQL("/graphql")
        .WithOptions(o => o.Tool.Enable = app.Environment.IsDevelopment());

    // Runs the host when there are no arguments, and HotChocolate's CLI when there are - which
    // is what lets CI produce the SDL with `dotnet run -- schema export --output schema.graphql`
    // instead of standing the service up and introspecting it over HTTP. The command builds the
    // schema through this same DI graph, so it needs ConnectionStrings:MetricsDb to be set - any
    // syntactically valid value will do, because nothing ever connects.
    return await app.RunWithGraphQLCommandsAsync(args);
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "The Gateway terminated unexpectedly during startup.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
