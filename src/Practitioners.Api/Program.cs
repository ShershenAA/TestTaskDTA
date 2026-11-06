using Microsoft.OpenApi.Models;
using Practitioners.Api.Data;
using Practitioners.Api.Infrastructure;
using Practitioners.Api.Messaging;

// Режим запуска: "api" (по умолчанию) или "consumer". Задаётся переменной окружения AppMode или --AppMode=consumer.
var mode = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build()["AppMode"]?.ToLowerInvariant() ?? "api";

if (mode == "consumer")
{
    var hostBuilder = Host.CreateApplicationBuilder(args);
    hostBuilder.Services.AddInfrastructure(hostBuilder.Configuration);
    hostBuilder.Services.AddHostedService<ImportConsumer>();
    await hostBuilder.Build().RunAsync();
}
else
{
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
            Description = "Справочник врачей: REST CRUD, FHIR R5 GET (/fhir/Practitioner), асинхронный импорт через Redis Streams."
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
}
