using System.Text.Json;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

const string paymentId = "00000000-0000-0000-0000-000000000001";
const string appointmentId = "11111111-1111-1111-1111-111111111111";
const string createdAt = "2026-01-15T10:00:00+00:00";
const string updatedAt = "2026-01-15T10:05:00+00:00";

var port = GetPort();
using var server = WireMockServer.Start(new WireMockServerSettings
{
    Port = port,
    StartAdminInterface = true,
    ReadStaticMappings = false
});

MapHealthEndpoint(server);
MapPaymentEndpoints(server);

Console.WriteLine($"Payment Service WireMock listening on http://localhost:{port}");
Console.WriteLine("Press Ctrl+C to stop.");

await Task.Delay(Timeout.InfiniteTimeSpan);

static int GetPort()
{
    var value = Environment.GetEnvironmentVariable("WIREMOCK_PORT");
    return int.TryParse(value, out var port) && port is > 0 and <= 65535 ? port : 5127;
}

static void MapHealthEndpoint(WireMockServer server)
{
    server
        .Given(Request.Create().WithPath("/health").UsingGet())
        .RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBodyAsJson(new { status = "Healthy" }));
}

static void MapPaymentEndpoints(WireMockServer server)
{
    var pending = PaymentResponse("Pending");
    var paid = PaymentResponse("Paid", processedAt: updatedAt);
    var failed = PaymentResponse("Failed", processedAt: updatedAt, failureReason: "Cartão recusado pela operadora");

    server
        .Given(Request.Create().WithPath("/api/payments").UsingGet())
        .RespondWith(JsonResponse(200, new[] { pending, paid, failed }));

    server
        .Given(Request.Create()
            .WithPath($"/api/payments/{paymentId}")
            .UsingGet())
        .RespondWith(JsonResponse(200, pending));

    server
        .Given(Request.Create()
            .WithPath("/api/payments/00000000-0000-0000-0000-000000000000")
            .UsingGet())
        .RespondWith(JsonResponse(404, new { message = "Pagamento não encontrado." }));

    server
        .Given(Request.Create()
            .WithPath("/api/payments")
            .UsingPost()
            .WithBody(new JsonMatcher("""
                {
                  "appointmentId": "00000000-0000-0000-0000-000000000000",
                  "amount": 0
                }
                """)))
        .RespondWith(JsonResponse(400, new { message = "The AppointmentId field is required." }));

    server
        .Given(Request.Create().WithPath("/api/payments").UsingPost())
        .RespondWith(JsonResponse(201, pending));

    server
        .Given(Request.Create()
            .WithPath($"/api/payments/{paymentId}/status")
            .UsingPatch()
            .WithBody(new JsonMatcher("""{"status":"Paid"}""")))
        .RespondWith(JsonResponse(200, paid));

    server
        .Given(Request.Create()
            .WithPath($"/api/payments/{paymentId}/status")
            .UsingPatch()
            .WithBody(new JsonMatcher("""{"status":"Failed","reason":"Cartão recusado pela operadora"}""")))
        .RespondWith(JsonResponse(200, failed));

    server
        .Given(Request.Create()
            .WithPath("/api/payments/00000000-0000-0000-0000-000000000000/status")
            .UsingPatch())
        .RespondWith(JsonResponse(404, new { message = "Pagamento não encontrado." }));

    server
        .Given(Request.Create()
            .WithPath($"/api/payments/{paymentId}/status")
            .UsingPatch())
        .RespondWith(JsonResponse(400, new { message = "Status de pagamento inválido." }));
}

static object PaymentResponse(
    string status,
    string? processedAt = null,
    string? failureReason = null) =>
    new
    {
        id = paymentId,
        appointmentId,
        amount = 150.00m,
        status,
        createdAt,
        updatedAt,
        processedAt,
        failureReason
    };

static IResponseBuilder JsonResponse(int statusCode, object body) =>
    Response.Create()
        .WithStatusCode(statusCode)
        .WithHeader("Content-Type", "application/json")
        .WithBody(JsonSerializer.Serialize(body));
