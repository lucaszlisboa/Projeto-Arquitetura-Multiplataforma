namespace Dentist_service.DentistsServices.Domain;

/// <summary>
/// Value Object que representa o resultado de uma validação de domínio.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<string> _errors = new();

    public bool IsValid => _errors.Count == 0;
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    public void AddError(string message) => _errors.Add(message);

    /// <summary>
    /// Resultado de sucesso sem erros.
    /// </summary>
    public static ValidationResult Success() => new();

    /// <summary>
    /// Resultado com um único erro.
    /// </summary>
    public static ValidationResult Failure(string error)
    {
        var result = new ValidationResult();
        result.AddError(error);
        return result;
    }
}
