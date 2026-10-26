using System.Collections.Concurrent;
using PaymentService.Domain;
using PaymentService.Domain.Ports;

namespace PaymentService.Infrastructure.Repositories;

public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _payments.TryGetValue(id, out var payment);
        return Task.FromResult(payment);
    }

    public Task<Payment?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_payments.Values.FirstOrDefault(payment => payment.AppointmentId == appointmentId));

    public Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Payment>>(_payments.Values
            .OrderByDescending(payment => payment.CreatedAt)
            .ToList());

    public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (!_payments.TryAdd(payment.Id, payment))
            throw new InvalidOperationException($"O pagamento '{payment.Id}' já existe.");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        _payments[payment.Id] = payment;
        return Task.CompletedTask;
    }
}
