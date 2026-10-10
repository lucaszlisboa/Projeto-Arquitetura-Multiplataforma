using Dentist_service.DentistsServices.Application.DTOs;

namespace Dentist_service.DentistsServices.Domain.Ports;

/// <summary>
/// Porta de entrada do domínio: contrato dos casos de uso para DentistServices.
/// Implementações concretas ficam na camada de aplicação.
/// </summary>
public interface IDentistServicesUseCase
{
    /// <summary>Busca um serviço por Id, garantindo que pertença ao dentista informado.</summary>
    Task<DentistServicesResponse> GetByIdAsync(Guid id, Guid dentistId, CancellationToken cancellationToken = default);

    /// <summary>Lista todos os serviços do dentista informado.</summary>
    Task<IEnumerable<DentistServicesResponse>> GetAllByDentistAsync(Guid dentistId, CancellationToken cancellationToken = default);

    Task<DentistServicesResponse> CreateAsync(CreateDentistServicesRequest request, CancellationToken cancellationToken = default);

    Task<DentistServicesResponse> UpdateAsync(Guid id, UpdateDentistServicesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Remove um serviço, garantindo que pertença ao dentista informado.</summary>
    Task DeleteAsync(Guid id, Guid dentistId, CancellationToken cancellationToken = default);
}
