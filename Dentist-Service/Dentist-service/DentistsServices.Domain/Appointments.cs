namespace Dentist_service.DentistsServices.Domain;

public enum AppointmentStatus { Pending, Confirmed, Rejected, Cancelled }
public enum AppointmentType { Private, Insurance }
public enum AppointmentActor { Dentist, Patient }

public class DentistWeeklyHours
{
    public Guid Id { get; private set; }
    public Guid DentistId { get; private set; }
    public int DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    private DentistWeeklyHours() { }
    public DentistWeeklyHours(Guid dentistId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    { Id = Guid.NewGuid(); DentistId = dentistId; DayOfWeek = dayOfWeek; StartTime = startTime; EndTime = endTime; }
}

public class DentistScheduleException
{
    public Guid Id { get; private set; }
    public Guid DentistId { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsClosed { get; private set; }
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    private DentistScheduleException() { }
    public DentistScheduleException(Guid dentistId, DateOnly date, bool isClosed, TimeOnly? startTime, TimeOnly? endTime)
    { Id = Guid.NewGuid(); DentistId = dentistId; Date = date; IsClosed = isClosed; StartTime = startTime; EndTime = endTime; }
}

public class Appointment
{
    public Guid Id { get; private set; }
    public Guid DentistId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid ServiceId { get; private set; }
    public DateTime StartAtUtc { get; private set; }
    public DateTime EndAtUtc { get; private set; }
    public AppointmentType Type { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public int PriceCents { get; private set; }
    public string? InsuranceName { get; private set; }
    public string? InsuranceCardNumber { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    private Appointment() { }
    public Appointment(Guid dentistId, Guid patientId, Guid serviceId, DateTime startAtUtc, DateTime endAtUtc,
        AppointmentType type, int priceCents, string? insuranceName, string? insuranceCardNumber)
    {
        Id = Guid.NewGuid(); DentistId = dentistId; PatientId = patientId; ServiceId = serviceId;
        StartAtUtc = startAtUtc; EndAtUtc = endAtUtc; Type = type; PriceCents = priceCents;
        InsuranceName = insuranceName; InsuranceCardNumber = insuranceCardNumber;
        Status = AppointmentStatus.Pending; CreatedAtUtc = DateTime.UtcNow;
    }
    public void SetStatus(AppointmentStatus status) => Status = status;
    public void UpdateSchedule(Guid serviceId, DateTime startAtUtc, DateTime endAtUtc, AppointmentType type, int priceCents, string? insuranceName, string? insuranceCardNumber)
    {
        ServiceId = serviceId;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        Type = type;
        PriceCents = priceCents;
        InsuranceName = insuranceName;
        InsuranceCardNumber = insuranceCardNumber;
    }
    public bool BlocksTime => Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;
}
