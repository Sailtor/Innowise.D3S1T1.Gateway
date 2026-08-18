using Gateway.Application;
using Gateway.Infrastructure;
using Gateway.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPresentation(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();

// Phase 3: app.MapGraphQL() and the /health endpoint.
app.Run();
