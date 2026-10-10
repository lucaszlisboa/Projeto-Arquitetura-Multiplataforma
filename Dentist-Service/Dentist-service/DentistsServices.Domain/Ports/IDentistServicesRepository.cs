namespace Dentist_service.DentistsServices.Domain.Ports;

/// <summary>
/// Porta de saída do domínio: contrato de persistência para DentistServices.
/// Implementações concretas ficam na camada de infraestrutura.
/// </summary>
public interface IDentistServicesRepository
{
    Task<DentistServices?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<DentistServices>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DentistServices>> GetByDentistIdAsync(Guid dentistId, CancellationToken cancellationToken = default);
    Task AddAsync(DentistServices dentistServices, CancellationToken cancellationToken = default);
    Task UpdateAsync(DentistServices dentistServices, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
