using Gateway.Application;
using Gateway.Infrastructure;
using Gateway.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPresentation(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();

app.MapHealthChecks("/health");

// Nitro (the built-in IDE) is the API documentation for this service, but it has no business being
// served in production. Introspection follows the same rule: HotChocolate disables it outside
// Development by default, which Phase 5 revisits for the compose demo.
app.MapGraphQL("/graphql")
    .WithOptions(o => o.Tool.Enable = app.Environment.IsDevelopment());

app.Run();
