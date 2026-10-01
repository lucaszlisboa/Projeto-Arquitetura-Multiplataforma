namespace Dentist_service.DentistsServices.Domain;

public class DentistServices
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid DentistId { get; private set; }
    public int Price { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime DeletedAt { get; private set; }
    public bool Isperiodic { get; private set; }
    public DentistServiceStatus Status { get; private set; }

    public DentistServices(
        Guid id,
        string name,
        Guid dentistId,
        int price,
        bool isPeriodic,
        DentistServiceStatus status)
    {
        Id = id;
        Name = name;
        DentistId = dentistId;
        Price = price;
        Isperiodic = isPeriodic;
        Status = status;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}