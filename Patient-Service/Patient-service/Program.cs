using Patient_service.Patients.Application.UseCases;
using Patient_service.Patients.Domain.Ports;
using Patient_service.Patients.Domain.Validators;
using Patient_service.Patients.Infrastructure.Data;
using Patient_service.Patients.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// Infraestrutura: EF Core + PostgreSQL
// ──────────────────────────────────────────────
builder.Services.AddDbContext<PatientDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PatientDbContext>("postgres");

// ──────────────────────────────────────────────
// Portas → Adaptadores (Hexagonal)
// ──────────────────────────────────────────────

// Porta de saída: repositório
builder.Services.AddScoped<IPatientRepository, PatientRepository>();

// Porta de entrada: validação
builder.Services.AddScoped<IPatientValidator, PatientValidator>();

// Porta de entrada: caso de uso
builder.Services.AddScoped<IPatientUseCase, PatientUseCase>();

// ──────────────────────────────────────────────
// API
// ──────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Permite enviar o status como string ("ativo") em vez de número inteiro
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

var app = builder.Build();

// Cria/atualiza o schema ao iniciar em um banco novo.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PatientDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
