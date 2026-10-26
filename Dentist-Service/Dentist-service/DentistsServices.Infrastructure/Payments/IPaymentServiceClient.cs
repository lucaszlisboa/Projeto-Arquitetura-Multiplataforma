using Dentist_service.DentistsServices.Domain;

namespace Dentist_service.DentistsServices.Infrastructure.Payments;

public interface IPaymentServiceClient
{
    Task<PaymentResponse> CreateAsync(Appointment appointment, CancellationToken cancellationToken = default);
}

public sealed record PaymentResponse(
    Guid Id,
    Guid AppointmentId,
    decimal Amount,
    string Status);
