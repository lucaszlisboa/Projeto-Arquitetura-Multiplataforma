namespace Dentist_service.DentistsServices.Domain.Ports;

/// <summary>
/// Porta de entrada do domínio: contrato de validação para DentistServices.
/// Implementações concretas ficam na camada de infraestrutura/aplicação.
/// </summary>
public interface IDentistServicesValidator
{
    /// <summary>
    /// Valida a entidade e retorna o resultado da validação.
    /// </summary>
    ValidationResult Validate(DentistServices dentistServices);
}
