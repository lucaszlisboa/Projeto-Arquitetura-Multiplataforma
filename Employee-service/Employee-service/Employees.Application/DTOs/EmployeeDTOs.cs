using Employee_service.Employees.Domain;

namespace Employee_service.Employees.Application.DTOs;

// ──────────────────────────────────────────────
// Requests
// ──────────────────────────────────────────────

/// <summary>
/// Payload para cadastrar um novo funcionário.
/// </summary>
public record CreateEmployeeRequest(
    string Name,
    string Cpf,
    string Email,
    string Phone,
    EmployeeRole Role,
    EmployeeStatus Status
);

/// <summary>
/// Payload para editar um funcionário existente.
/// O CPF não faz parte da edição: ele identifica o funcionário e é imutável.
/// </summary>
public record UpdateEmployeeRequest(
    string Name,
    string Email,
    string Phone,
    EmployeeRole Role,
    EmployeeStatus Status
);

/// <summary>Solicita ao Dentist Service a alteração de um agendamento.</summary>
public sealed record UpdateAppointmentRequest(
    Guid DentistId,
    Guid ServiceId,
    string StartAt,
    string Type,
    string? InsuranceName,
    string? InsuranceCardNumber
);

/// <summary>Dados do agendamento retornados pelo Dentist Service.</summary>
public sealed record AppointmentResponse(
    Guid Id,
    Guid DentistId,
    Guid PatientId,
    Guid ServiceId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Type,
    string Status,
    int PriceCents,
    string? InsuranceName,
    string? InsuranceCardNumber,
    DateTime CreatedAtUtc
);

// ──────────────────────────────────────────────
// Response
// ──────────────────────────────────────────────

/// <summary>
/// Dados retornados ao cliente após operações de leitura/escrita.
/// </summary>
public record EmployeeResponse(
    Guid Id,
    string Name,
    string Cpf,
    string Email,
    string Phone,
    EmployeeRole Role,
    EmployeeStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
