using Dentist_service.DentistsServices.Application.UseCases;
using Dentist_service.DentistsServices.Domain.Ports;
using Dentist_service.DentistsServices.Domain.Validators;
using Dentist_service.DentistsServices.Infrastructure.Data;
using Dentist_service.DentistsServices.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// Infraestrutura: EF Core + PostgreSQL
// ──────────────────────────────────────────────
builder.Services.AddDbContext<DentistServicesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ──────────────────────────────────────────────
// Portas → Adaptadores (Hexagonal)
// ──────────────────────────────────────────────

// Porta de saída: repositório
builder.Services.AddScoped<IDentistServicesRepository, DentistServicesRepository>();

// Porta de entrada: validação
builder.Services.AddScoped<IDentistServicesValidator, DentistServicesValidator>();

// Porta de entrada: caso de uso
builder.Services.AddScoped<IDentistServicesUseCase, DentistServicesUseCase>();

// ──────────────────────────────────────────────
// API
// ──────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
