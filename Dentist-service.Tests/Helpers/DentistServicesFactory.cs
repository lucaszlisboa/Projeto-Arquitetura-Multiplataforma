using Dentist_service.DentistsServices.Domain;

namespace Dentist_service.Tests.Helpers;

/// <summary>
/// Factory centralizada para construção de entidades DentistServices nos testes.
/// Evita duplicação de código de setup e garante valores padrão consistentes.
/// </summary>
public static class DentistServicesFactory
{
    public static readonly Guid DefaultId        = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid DefaultDentistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid OtherDentistId   = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    /// <summary>
    /// Cria uma entidade válida com os valores padrão ou sobrescrevendo os campos desejados.
    /// </summary>
    public static DentistServices Create(
        Guid?                  id         = null,
        string?                name       = null,
        Guid?                  dentistId  = null,
        int?                   price      = null,
        bool                   isPeriodic = true,
        DentistServiceStatus   status     = DentistServiceStatus.disponivel)
    {
        return new DentistServices(
            id:         id        ?? DefaultId,
            name:       name      ?? "Limpeza dental",
            dentistId:  dentistId ?? DefaultDentistId,
            price:      price     ?? 150,
            isPeriodic: isPeriodic,
            status:     status);
    }
}
