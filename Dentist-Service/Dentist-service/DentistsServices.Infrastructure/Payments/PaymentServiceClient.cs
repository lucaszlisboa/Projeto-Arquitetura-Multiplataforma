using System.Net.Http.Json;
using System.Net;
using Dentist_service.DentistsServices.Domain;

namespace Dentist_service.DentistsServices.Infrastructure.Payments;

public sealed class PaymentServiceClient(
    HttpClient httpClient,
    ILogger<PaymentServiceClient> logger) : IPaymentServiceClient
{
    public async Task<PaymentResponse> CreateAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            appointmentId = appointment.Id,
            amount = appointment.PriceCents / 100m
        };

        using var response = await httpClient.PostAsJsonAsync(
            "api/payments",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Payment Service rejeitou o pagamento do agendamento {AppointmentId}: HTTP {StatusCode} {Body}",
                appointment.Id,
                (int)response.StatusCode,
                body);
            throw new PaymentServiceException(response.StatusCode, body);
        }

        return await response.Content.ReadFromJsonAsync<PaymentResponse>(cancellationToken: cancellationToken)
            ?? throw new PaymentServiceException(response.StatusCode, "Resposta vazia do Payment Service.");
    }
}

public sealed class PaymentServiceException(HttpStatusCode statusCode, string body) : Exception(body)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
