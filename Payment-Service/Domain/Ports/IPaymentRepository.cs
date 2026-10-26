using PaymentService.Domain;

namespace PaymentService.Domain.Ports;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Payment?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
}
