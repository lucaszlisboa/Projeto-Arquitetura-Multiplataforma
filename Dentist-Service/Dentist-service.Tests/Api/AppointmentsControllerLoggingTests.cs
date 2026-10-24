using Dentist_service.DentistsServices.Api.Controllers;
using Dentist_service.DentistsServices.Api.Telemetry;
using Dentist_service.DentistsServices.Domain;
using Dentist_service.DentistsServices.Domain.Ports;
using Dentist_service.DentistsServices.Infrastructure.Data;
using Dentist_service.DentistsServices.Infrastructure.Payments;
using Dentist_service.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace Dentist_service.Tests.Api;

public class AppointmentsControllerLoggingTests : IDisposable
{
    private static readonly TimeZoneInfo ClinicZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly Guid PatientId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private const string InsuranceCardNumber = "CARTEIRINHA-SECRETA-999";

    private readonly DentistServicesDbContext _db;
    private readonly FakeLogger<AppointmentsController> _logger = new();
    private readonly Mock<IAppointmentEventPublisher> _events = new();
    private readonly Mock<IPaymentServiceClient> _paymentService = new();
    private readonly AppointmentsController _controller;

    public AppointmentsControllerLoggingTests()
    {
        var options = new DbContextOptionsBuilder<DentistServicesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new DentistServicesDbContext(options);
        _db.DentistServices.Add(DentistServicesFactory.Create());
        for (var day = 0; day <= 6; day++)
            _db.WeeklyHours.Add(new DentistWeeklyHours(DentistServicesFactory.DefaultDentistId, day, new TimeOnly(8, 0), new TimeOnly(18, 0)));
        _db.SaveChanges();

        _controller = new AppointmentsController(_db, _events.Object, _paymentService.Object, new AppointmentMetrics(), _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_QuandoPacienteSolicitaHorarioLivre_DeveLogarRequestedSemDadosSensiveis()
    {
        AsPatient();

        var result = await _controller.Create(NewRequest(Hour(10)), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
        var log = SingleLog("appointment.requested");
        log.Level.Should().Be(LogLevel.Information);
        log.StructuredState!.Should().Contain(p => p.Key == "Actor" && p.Value == "Patient");
        log.StructuredState!.Should().Contain(p => p.Key == "AppointmentType" && p.Value == "Private");
        AllLogText().Should().NotContain(InsuranceCardNumber);
    }

    [Fact]
    public async Task Create_QuandoConvenio_NaoDeveLogarNumeroDaCarteirinha()
    {
        AsPatient();

        await _controller.Create(NewRequest(Hour(10), "Insurance", "Unimed", InsuranceCardNumber), CancellationToken.None);

        SingleLog("appointment.requested");
        AllLogText().Should().NotContain(InsuranceCardNumber).And.NotContain("Unimed");
    }

    [Fact]
    public async Task Create_QuandoHorarioJaOcupado_DeveLogarSlotConflictComoWarning()
    {
        AsPatient();
        await _controller.Create(NewRequest(Hour(10)), CancellationToken.None);

        var result = await _controller.Create(NewRequest(Hour(10)), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        var log = SingleLog("appointment.slot_conflict");
        log.Level.Should().Be(LogLevel.Warning);
        log.StructuredState!.Should().Contain(p => p.Key == "Reason" && p.Value == "overlap");
    }

    [Fact]
    public async Task Create_QuandoForaDoExpediente_DeveLogarSlotConflictComMotivoDeDisponibilidade()
    {
        AsPatient();

        await _controller.Create(NewRequest(Hour(3)), CancellationToken.None);

        SingleLog("appointment.slot_conflict").StructuredState!
            .Should().Contain(p => p.Key == "Reason" && p.Value == "outside_availability");
    }

    [Fact]
    public async Task Create_QuandoServicoPertenceAOutroDentista_DeveLogarDenied()
    {
        Headers("X-Dentist-Id", DentistServicesFactory.OtherDentistId.ToString());

        var result = await _controller.Create(NewRequest(Hour(10)) with { PatientId = PatientId }, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        SingleLog("appointment.denied").StructuredState!
            .Should().Contain(p => p.Key == "Reason" && p.Value == "service_not_owned_by_dentist");
    }

    [Fact]
    public async Task Confirm_QuandoPedidoPendente_DeveLogarConfirmed()
    {
        var appointment = await SeedAppointmentAsync(Hour(10));
        AsDentist();

        var result = await _controller.Confirm(appointment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        SingleLog("appointment.confirmed").StructuredState!
            .Should().Contain(p => p.Key == "AppointmentId" && p.Value == appointment.Id.ToString());
    }

    [Fact]
    public async Task Confirm_QuandoHaOutroPedidoNoMesmoHorario_DeveLogarSlotConflictComAgendamento()
    {
        var first = await SeedAppointmentAsync(Hour(10));
        await SeedAppointmentAsync(Hour(10));
        AsDentist();

        var result = await _controller.Confirm(first.Id, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        var log = SingleLog("appointment.slot_conflict");
        log.StructuredState!.Should().Contain(p => p.Key == "AppointmentId" && p.Value == first.Id.ToString());
    }

    [Fact]
    public async Task Reject_QuandoPedidoPendente_DeveLogarRejected()
    {
        var appointment = await SeedAppointmentAsync(Hour(10));
        AsDentist();

        await _controller.Reject(appointment.Id, CancellationToken.None);

        SingleLog("appointment.rejected");
    }

    [Fact]
    public async Task Cancel_QuandoPacienteCancelaPedidoPendente_DeveLogarCancelledComAtorPaciente()
    {
        var appointment = await SeedAppointmentAsync(Hour(10));
        AsPatient();

        await _controller.Cancel(appointment.Id, CancellationToken.None);

        SingleLog("appointment.cancelled").StructuredState!
            .Should().Contain(p => p.Key == "Actor" && p.Value == "Patient");
    }

    [Fact]
    public async Task Cancel_QuandoPacienteCancelaConfirmadaEmMenosDe24h_DeveLogarDeniedSemCancelar()
    {
        var appointment = await SeedAppointmentAsync(DateTime.UtcNow.AddHours(2), AppointmentStatus.Confirmed);
        AsPatient();

        var result = await _controller.Cancel(appointment.Id, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        SingleLog("appointment.denied").StructuredState!
            .Should().Contain(p => p.Key == "Reason" && p.Value == "cancellation_too_late");
        _logger.Collector.GetSnapshot().Should().NotContain(l => l.Id.Name == "appointment.cancelled");
    }

    [Fact]
    public async Task Availability_QuandoConsultaAgenda_DeveLogarDebugComQuantidadeDeHorarios()
    {
        var result = await _controller.Availability(
            DentistServicesFactory.DefaultDentistId, DentistServicesFactory.DefaultId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var log = SingleLog("availability.queried");
        log.Level.Should().Be(LogLevel.Debug);
        log.StructuredState!.Should().Contain(p => p.Key == "SlotCount" && int.Parse(p.Value!) > 0);
    }

    [Fact]
    public async Task Availability_QuandoDentistaConsultaAgendaDeOutro_DeveLogarDenied()
    {
        Headers("X-Dentist-Id", DentistServicesFactory.OtherDentistId.ToString());

        var result = await _controller.Availability(
            DentistServicesFactory.DefaultDentistId, DentistServicesFactory.DefaultId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        SingleLog("appointment.denied").StructuredState!
            .Should().Contain(p => p.Key == "Reason" && p.Value == "dentist_foreign_schedule");
    }

    [Fact]
    public async Task SetException_QuandoValida_DeveLogarExceptionUpdated()
    {
        AsDentist();
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicZone)).AddDays(3);

        await _controller.SetException(new AppointmentsController.ExceptionRequest(date, true, null, null), CancellationToken.None);

        SingleLog("schedule.exception_updated").StructuredState!
            .Should().Contain(p => p.Key == "IsClosed" && p.Value == "True");
    }

    private static DateTime Hour(int localHour)
    {
        var tomorrow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicZone).Date.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(tomorrow.AddHours(localHour), ClinicZone);
    }

    private static AppointmentsController.CreateAppointmentRequest NewRequest(
        DateTime startUtc, string type = "Private", string? insuranceName = null, string? insuranceCard = null) =>
        new(DentistServicesFactory.DefaultId, startUtc.ToString("O"), type, insuranceName, insuranceCard, null);

    private async Task<Appointment> SeedAppointmentAsync(DateTime startUtc, AppointmentStatus status = AppointmentStatus.Pending)
    {
        var appointment = new Appointment(
            DentistServicesFactory.DefaultDentistId, PatientId, DentistServicesFactory.DefaultId,
            startUtc, startUtc.AddMinutes(30), AppointmentType.Private, 150, null, null);
        appointment.SetStatus(status);
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        return appointment;
    }

    private void AsPatient() => Headers("X-Patient-Id", PatientId.ToString());

    private void AsDentist() => Headers("X-Dentist-Id", DentistServicesFactory.DefaultDentistId.ToString());

    private void Headers(string name, string value) =>
        _controller.ControllerContext.HttpContext.Request.Headers[name] = value;

    private FakeLogRecord SingleLog(string eventName) =>
        _logger.Collector.GetSnapshot().Where(l => l.Id.Name == eventName).Should().ContainSingle().Subject;

    private string AllLogText() =>
        string.Join('\n', _logger.Collector.GetSnapshot().Select(l =>
            l.Message + " " + string.Join(' ', l.StructuredState?.Select(p => $"{p.Key}={p.Value}") ?? [])));
}
