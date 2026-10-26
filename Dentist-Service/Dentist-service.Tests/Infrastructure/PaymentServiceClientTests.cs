using Dentist_service.DentistsServices.Domain;
using Dentist_service.DentistsServices.Infrastructure.Payments;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace Dentist_service.Tests.Infrastructure;

public sealed class PaymentServiceClientTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start(new WireMockServerSettings
    {
        Port = 0,
        ReadStaticMappings = false
    });

    public void Dispose() => _server.Stop();

    [Fact]
    public async Task CreateAsync_UsesWireMockResponse()
    {
        var appointmentId = Guid.NewGuid();
        _server
            .Given(Request.Create().WithPath("/api/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = Guid.NewGuid(),
                    appointmentId,
                    amount = 150.00m,
                    status = "Pending"
                }));

        using var httpClient = new HttpClient { BaseAddress = new Uri(_server.Url + "/") };
        var client = new PaymentServiceClient(
            httpClient,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentServiceClient>.Instance);
        var appointment = new Appointment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddMinutes(30),
            AppointmentType.Private,
            15000,
            null,
            null);

        var response = await client.CreateAsync(appointment);

        response.AppointmentId.Should().Be(appointmentId);
        response.Amount.Should().Be(150m);
        response.Status.Should().Be("Pending");
        _server.LogEntries.Should().ContainSingle(entry =>
            entry.RequestMessage.Method == "POST" &&
            entry.RequestMessage.Path == "/api/payments");
    }
}
