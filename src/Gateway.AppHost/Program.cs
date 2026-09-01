using Gateway.AppHost;
using Gateway.Application;
using Gateway.Infrastructure;
using Gateway.Presentation;
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

    app.UseHttpsRedirection();
    app.UseCors();

    app.MapHealthChecks("/health");

    // Nitro (the built-in IDE) is this service's API documentation, but it has no business being
    // served in production. Introspection is governed separately by GraphQL:AllowIntrospection.
    app.MapGraphQL("/graphql")
        .WithOptions(o => o.Tool.Enable = app.Environment.IsDevelopment());

    app.Run();
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
