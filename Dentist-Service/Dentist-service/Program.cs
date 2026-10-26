using Dentist_service.DentistsServices.Api.Telemetry;
using Dentist_service.DentistsServices.Application.UseCases;
using Dentist_service.DentistsServices.Domain.Ports;
using Dentist_service.DentistsServices.Domain.Validators;
using Dentist_service.DentistsServices.Infrastructure.Data;
using Dentist_service.DentistsServices.Infrastructure.Events;
using Dentist_service.DentistsServices.Infrastructure.Repositories;
using Messaging;
using Messaging.Telemetry;
using Microsoft.EntityFrameworkCore;
using Observability;
using Dentist_service.DentistsServices.Infrastructure.Payments;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.AddOdontoLogging();

// ──────────────────────────────────────────────
// Infraestrutura: EF Core + PostgreSQL
// ──────────────────────────────────────────────
builder.Services.AddDbContext<DentistServicesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddKafkaMessaging(builder.Configuration)
    .AddOutbox<DentistServicesDbContext>()
    .AddConsumer();

// ──────────────────────────────────────────────
// Portas → Adaptadores (Hexagonal)
// ──────────────────────────────────────────────

// Porta de saída: repositório
builder.Services.AddScoped<IDentistServicesRepository, DentistServicesRepository>();

// Porta de entrada: validação
builder.Services.AddScoped<IDentistServicesValidator, DentistServicesValidator>();

// Porta de entrada: caso de uso
builder.Services.AddScoped<IDentistServicesUseCase, DentistServicesUseCase>();

builder.Services.AddScoped<IAppointmentEventPublisher, AppointmentEventPublisher>();
builder.Services.AddHttpClient<IPaymentServiceClient, PaymentServiceClient>(client =>
{
    var baseUrl = builder.Configuration["PaymentService:BaseUrl"] ?? "http://localhost:5127";
    client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/");
});

// ──────────────────────────────────────────────
// Observabilidade: métricas de negócio + Prometheus
// ──────────────────────────────────────────────
builder.Services.AddSingleton<AppointmentMetrics>();
builder.Services.AddHostedService<AppointmentMetricsSampler>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("dentist-service"))
    .WithMetrics(metrics => metrics
        .AddMeter(AppointmentMetrics.MeterName)
        .AddMeter(MessagingMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<DentistServicesDbContext>("postgres");

// ──────────────────────────────────────────────
// API
// ──────────────────────────────────────────────
builder.Services.AddControllers(options =>
    {
        // Captura 400 de validação de modelo que acontecem antes da action.
        options.Filters.Add<AppointmentModelValidationFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Permite enviar o status como string ("disponivel") em vez de número inteiro
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

var app = builder.Build();

// Aplica as migrations no boot (garante o schema em um banco novo).
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DentistServicesDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Em produção mantém o redirecionamento para HTTPS. Em desenvolvimento ele é
    // dispensado para que o Prometheus possa raspar /metrics via HTTP.
    app.UseHttpsRedirection();
}

app.UseOdontoObservability();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

app.Run();
