using Microsoft.EntityFrameworkCore;
using PaymentService.Domain;
using PaymentService.Domain.Ports;
using PaymentService.Infrastructure.Data;

namespace PaymentService.Infrastructure.Repositories;

public sealed class PostgresPaymentRepository(PaymentDbContext context) : IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        context.Payments.FirstOrDefaultAsync(payment => payment.Id == id, cancellationToken);

    public Task<Payment?> GetByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default) =>
        context.Payments.FirstOrDefaultAsync(payment => payment.AppointmentId == appointmentId, cancellationToken);

    public async Task<IReadOnlyList<Payment>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await context.Payments
            .AsNoTracking()
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        await context.Payments.AddAsync(payment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
