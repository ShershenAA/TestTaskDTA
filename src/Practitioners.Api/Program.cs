using Microsoft.OpenApi.Models;
using Practitioners.Api.Data;
using Practitioners.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Practitioner Directory API",
        Version = "v1",
        Description = "Справочник врачей: FHIR R5 GET (/fhir/Practitioner)."
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "Practitioners.Api.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});
builder.Services.AddHealthChecks();

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, app.Logger);

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
