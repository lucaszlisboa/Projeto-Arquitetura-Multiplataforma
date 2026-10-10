namespace Dentist_service.DentistsServices.Domain;

public class DentistServices
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid DentistId { get; private set; }
    public int Price { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime DeletedAt { get; private set; }
    public bool Isperiodic { get; private set; }
    public DentistServiceStatus Status { get; private set; }

    /// <summary>
    /// Construtor protegido exigido pelo EF Core para materialização de entidades.
    /// Não usar diretamente no código de aplicação.
    /// </summary>
    protected DentistServices() { }

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

    /// <summary>
    /// Atualiza os dados mutáveis do serviço, preservando Id, DentistId e CreatedAt.
    /// </summary>
    public void Update(string name, int price, bool isPeriodic, DentistServiceStatus status)
    {
        Name = name;
        Price = price;
        Isperiodic = isPeriodic;
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Verifica se o serviço pertence ao dentista informado.
    /// Regra de negócio de ownership centralizada na entidade.
    /// </summary>
    public bool IsOwnedBy(Guid dentistId) => DentistId == dentistId;

    /// <summary>
    /// Marca o serviço como deletado (soft delete).
    /// </summary>
    public void MarkAsDeleted()
    {
        DeletedAt = DateTime.UtcNow;
    }
}