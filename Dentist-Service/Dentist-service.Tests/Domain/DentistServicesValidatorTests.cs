using Dentist_service.DentistsServices.Domain;
using Dentist_service.DentistsServices.Domain.Validators;
using Dentist_service.Tests.Helpers;
using FluentAssertions;

namespace Dentist_service.Tests.Domain;

/// <summary>
/// Testes unitários para DentistServicesValidator.
/// Cobre: entidade válida, nome inválido, preço inválido, dentistId vazio.
/// </summary>
public class DentistServicesValidatorTests
{
    private readonly DentistServicesValidator _validator = new();

    // ──────────────────────────────────────────
    // Cenário válido
    // ──────────────────────────────────────────

    [Fact]
    public void Validate_WhenEntityIsValid_ShouldReturnSuccess()
    {
        // Arrange
        var service = DentistServicesFactory.Create();

        // Act
        var result = _validator.Validate(service);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ──────────────────────────────────────────
    // Name
    // ──────────────────────────────────────────

    [Fact]
    public void Validate_WhenNameIsEmpty_ShouldReturnError()
    {
        // Arrange
        var service = DentistServicesFactory.Create(name: "");

        // Act
        var result = _validator.Validate(service);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("nome"));
    }

    [Fact]
    public void Validate_WhenNameIsTooShort_ShouldReturnError()
    {
        // Arrange
        var service = DentistServicesFactory.Create(name: "AB");

        // Act
        var result = _validator.Validate(service);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("3 caracteres"));
    }

    [Fact]
    public void Validate_WhenNameExceeds100Characters_ShouldReturnError()
    {
        // Arrange
        var longName = new string('A', 101);
        var service  = DentistServicesFactory.Create(name: longName);

        // Act
        var result = _validator.Validate(service);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("100 caracteres"));
    }

    // ──────────────────────────────────────────
    // Price
    // ──────────────────────────────────────────

    [Fact]
    public void Validate_WhenPriceIsZero_ShouldReturnError()
    {
        // Arrange
        var service = DentistServicesFactory.Create(price: 0);

        // Act
        var result = _validator.Validate(service);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("preço"));
    }

    [Fact]
    public void Validate_WhenPriceIsNegative_ShouldReturnError()
    {
        // Arrange
        var service = DentistServicesFactory.Create(price: -10);

        // Act
        var result = _validator.Validate(service);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("preço"));
    }

    // ──────────────────────────────────────────
    // DentistId
    // ──────────────────────────────────────────

    [Fact]
    public void Validate_WhenDentistIdIsEmpty_ShouldReturnError()
    {
        // Arrange
        var service = DentistServicesFactory.Create(dentistId: Guid.Empty);

        // Act
        var result = _validator.Validate(service);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("dentista"));
    }

    // ──────────────────────────────────────────
    // Status
    // ──────────────────────────────────────────

    [Theory]
    [InlineData(DentistServiceStatus.disponivel)]
    [InlineData(DentistServiceStatus.indisponivel)]
    [InlineData(DentistServiceStatus.emBreve)]
    public void Validate_WhenStatusIsValid_ShouldReturnSuccess(DentistServiceStatus status)
    {
        // Arrange
        var service = DentistServicesFactory.Create(status: status);

        // Act
        var result = _validator.Validate(service);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
