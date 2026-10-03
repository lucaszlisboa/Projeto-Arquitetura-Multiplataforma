using Dentist_service.DentistsServices.Application.DTOs;
using Dentist_service.DentistsServices.Domain;
using Dentist_service.DentistsServices.Domain.Exceptions;
using Dentist_service.DentistsServices.Domain.Ports;

namespace Dentist_service.DentistsServices.Application.UseCases;

/// <summary>
/// Application Service: implementa os casos de uso de DentistServices.
/// Depende apenas das portas definidas no domínio (hexagonal).
/// </summary>
public class DentistServicesUseCase : IDentistServicesUseCase
{
    private readonly IDentistServicesRepository _repository;
    private readonly IDentistServicesValidator _validator;

    public DentistServicesUseCase(
        IDentistServicesRepository repository,
        IDentistServicesValidator validator)
    {
        _repository = repository;
        _validator = validator;
    }

    // ──────────────────────────────────────────
    // Leitura
    // ──────────────────────────────────────────

    public async Task<DentistServicesResponse> GetByIdAsync(Guid id, Guid dentistId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Serviço com id '{id}' não encontrado.");

        if (!entity.IsOwnedBy(dentistId))
            throw new DomainException("Acesso negado: este serviço não pertence ao dentista informado.");

        return ToResponse(entity);
    }

    public async Task<IEnumerable<DentistServicesResponse>> GetAllByDentistAsync(Guid dentistId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByDentistIdAsync(dentistId, cancellationToken);
        return entities.Select(ToResponse);
    }

    // ──────────────────────────────────────────
    // Escrita
    // ──────────────────────────────────────────

    public async Task<DentistServicesResponse> CreateAsync(CreateDentistServicesRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new DentistServices(
            id: Guid.NewGuid(),
            name: request.Name,
            dentistId: request.DentistId,
            price: request.Price,
            isPeriodic: request.IsPeriodic,
            status: request.Status);

        var validationResult = _validator.Validate(entity);
        if (!validationResult.IsValid)
            throw new ArgumentException(string.Join("; ", validationResult.Errors));

        await _repository.AddAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public async Task<DentistServicesResponse> UpdateAsync(Guid id, UpdateDentistServicesRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Serviço com id '{id}' não encontrado.");

        entity.Update(request.Name, request.Price, request.IsPeriodic, request.Status);

        var validationResult = _validator.Validate(entity);
        if (!validationResult.IsValid)
            throw new ArgumentException(string.Join("; ", validationResult.Errors));

        await _repository.UpdateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public async Task DeleteAsync(Guid id, Guid dentistId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Serviço com id '{id}' não encontrado.");

        if (!entity.IsOwnedBy(dentistId))
            throw new DomainException("Acesso negado: este serviço não pertence ao dentista informado.");

        await _repository.DeleteAsync(entity.Id, cancellationToken);
    }

    // ──────────────────────────────────────────
    // Mapeamento
    // ──────────────────────────────────────────

    private static DentistServicesResponse ToResponse(DentistServices entity) =>
        new(entity.Id,
            entity.Name,
            entity.DentistId,
            entity.Price,
            entity.Isperiodic,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt);
}
