using Dentist_service.DentistsServices.Domain.Ports;
using FluentValidation;

namespace Dentist_service.DentistsServices.Domain.Validators;

/// <summary>
/// Implementação concreta do validator de DentistServices usando FluentValidation.
/// Implementa a porta IDentistServicesValidator definida no domínio.
/// </summary>
public class DentistServicesValidator : AbstractValidator<DentistServices>, IDentistServicesValidator
{
    public DentistServicesValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do serviço é obrigatório.")
            .MinimumLength(3).WithMessage("O nome deve ter ao menos 3 caracteres.")
            .MaximumLength(100).WithMessage("O nome não pode ultrapassar 100 caracteres.");

        RuleFor(x => x.DentistId)
            .NotEmpty().WithMessage("O ID do dentista é obrigatório.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("O preço deve ser maior que zero.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status inválido para o serviço.");
    }

    /// <summary>
    /// Valida a entidade e retorna o ValidationResult do domínio.
    /// </summary>
    public new ValidationResult Validate(DentistServices dentistServices)
    {
        var fluentResult = base.Validate(dentistServices);
        var result = new ValidationResult();

        foreach (var error in fluentResult.Errors)
            result.AddError(error.ErrorMessage);

        return result;
    }
}
