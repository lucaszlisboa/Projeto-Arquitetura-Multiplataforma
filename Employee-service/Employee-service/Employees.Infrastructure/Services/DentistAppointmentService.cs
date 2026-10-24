using System.Net;
using System.Net.Http.Json;
using Employee_service.Employees.Application.DTOs;
using Employee_service.Employees.Application.Services;

namespace Employee_service.Employees.Infrastructure.Services;

public sealed class DentistAppointmentService(HttpClient httpClient, ILogger<DentistAppointmentService> logger) : IAppointmentService
{
    public async Task<AppointmentResponse> UpdateAsync(Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"api/appointments/{id}")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("X-Dentist-Id", request.DentistId.ToString());

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Falha de comunicação com o Dentist Service ao atualizar o agendamento {AppointmentId}", id);
            throw new DentistServiceUnavailableException();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DentistServiceException(response.StatusCode, body);

        var result = System.Text.Json.JsonSerializer.Deserialize<AppointmentResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        return result ?? throw new DentistServiceException(HttpStatusCode.BadGateway, "Resposta inválida do Dentist Service.");
    }
}

public sealed class DentistServiceException(HttpStatusCode statusCode, string body) : Exception(body)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = body;
}

public sealed class DentistServiceUnavailableException() : Exception;
