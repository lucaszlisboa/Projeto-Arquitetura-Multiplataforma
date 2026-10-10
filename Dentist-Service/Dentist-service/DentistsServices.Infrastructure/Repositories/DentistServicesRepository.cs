using Dentist_service.DentistsServices.Domain;
using Dentist_service.DentistsServices.Domain.Ports;
using Dentist_service.DentistsServices.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dentist_service.DentistsServices.Infrastructure.Repositories;

/// <summary>
/// Adaptador de saída: implementa a porta IDentistServicesRepository usando EF Core + PostgreSQL.
/// </summary>
public class DentistServicesRepository : IDentistServicesRepository
{
    private readonly DentistServicesDbContext _context;

    public DentistServicesRepository(DentistServicesDbContext context)
    {
        _context = context;
    }

    public async Task<DentistServices?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DentistServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<DentistServices>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DentistServices
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DentistServices>> GetByDentistIdAsync(Guid dentistId, CancellationToken cancellationToken = default)
    {
        return await _context.DentistServices
            .AsNoTracking()
            .Where(s => s.DentistId == dentistId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DentistServices dentistServices, CancellationToken cancellationToken = default)
    {
        await _context.DentistServices.AddAsync(dentistServices, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DentistServices dentistServices, CancellationToken cancellationToken = default)
    {
        _context.DentistServices.Update(dentistServices);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.DentistServices
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (entity is not null)
        {
            _context.DentistServices.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
