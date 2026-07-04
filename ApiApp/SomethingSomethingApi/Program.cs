// WebApplication.CreateBuilder prepares dependency injection, configuration,
// logging, and command-line/environment support for the API project.
var builder = WebApplication.CreateBuilder(args);

// AddControllers enables attribute-based API controllers such as InventoryController.
builder.Services.AddControllers();

// These services generate Swagger/OpenAPI metadata so the endpoints can be tested in a browser.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS is enabled so the MAUI app can call the API during classroom development and testing.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        // This policy is intentionally open for local training/demo use.
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Build creates the actual ASP.NET Core application pipeline from the configured services.
var app = builder.Build();

// Swagger is only exposed automatically in Development so endpoint testing is easier while building.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// UseCors applies the named CORS policy to incoming requests.
app.UseCors("AllowAll");

// UseAuthorization keeps the standard middleware order in place, even though
// this classroom project does not yet add authentication/authorization rules.
app.UseAuthorization();

// MapControllers connects controller routes like /api/inventory to their action methods.
app.MapControllers();

// Run starts the API and keeps listening until the app is stopped.
app.Run();
