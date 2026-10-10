namespace Dentist_service.DentistsServices.Domain.Exceptions;

/// <summary>
/// Exceção de domínio lançada quando uma regra de negócio é violada.
/// Usada para sinalizar acesso não autorizado a um recurso (ownership).
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
