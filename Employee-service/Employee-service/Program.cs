using Employee_service.Employees.Application.UseCases;
using Employee_service.Employees.Application.Services;
using Employee_service.Employees.Domain.Ports;
using Employee_service.Employees.Domain.Validators;
using Employee_service.Employees.Infrastructure.Data;
using Employee_service.Employees.Infrastructure.Repositories;
using Employee_service.Employees.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// Infraestrutura: EF Core + PostgreSQL
// ──────────────────────────────────────────────
builder.Services.AddDbContext<EmployeeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ──────────────────────────────────────────────
// Portas → Adaptadores (Hexagonal)
// ──────────────────────────────────────────────

// Porta de saída: repositório
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();

// Porta de entrada: validação
builder.Services.AddScoped<IEmployeeValidator, EmployeeValidator>();

// Porta de entrada: caso de uso
builder.Services.AddScoped<IEmployeeUseCase, EmployeeUseCase>();
builder.Services.AddHttpClient<IAppointmentService, DentistAppointmentService>(client =>
{
    var baseUrl = builder.Configuration["DentistService:BaseUrl"] ?? "http://localhost:5068";
    client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/");
});

// ──────────────────────────────────────────────
// API
// ──────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Permite enviar cargo/status como string ("recepcionista", "ativo") em vez de número inteiro
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

var app = builder.Build();

// Aplica as migrations no boot (garante o schema em um banco novo).
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
