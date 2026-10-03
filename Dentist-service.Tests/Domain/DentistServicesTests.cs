using Dentist_service.DentistsServices.Domain;
using Dentist_service.Tests.Helpers;
using FluentAssertions;

namespace Dentist_service.Tests.Domain;

/// <summary>
/// Testes unitários para a entidade de domínio DentistServices.
/// Cobre: construtor, Update, IsOwnedBy, MarkAsDeleted.
/// </summary>
public class DentistServicesTests
{
    // ──────────────────────────────────────────
    // Construtor
    // ──────────────────────────────────────────

    [Fact]
    public void Constructor_WhenCalledWithValidData_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var id        = DentistServicesFactory.DefaultId;
        var dentistId = DentistServicesFactory.DefaultDentistId;

        // Act
        var service = DentistServicesFactory.Create(id: id, dentistId: dentistId, name: "Limpeza", price: 200);

        // Assert
        service.Id.Should().Be(id);
        service.DentistId.Should().Be(dentistId);
        service.Name.Should().Be("Limpeza");
        service.Price.Should().Be(200);
        service.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        service.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_WhenCalled_ShouldInitializeDeletedAtAsDefault()
    {
        // Arrange & Act
        var service = DentistServicesFactory.Create();

        // Assert
        service.DeletedAt.Should().Be(default(DateTime));
    }

    // ──────────────────────────────────────────
    // Update
    // ──────────────────────────────────────────

    [Fact]
    public void Update_WhenCalledWithValidData_ShouldUpdateMutableFields()
    {
        // Arrange
        var service = DentistServicesFactory.Create(name: "Nome antigo", price: 100);

        // Act
        service.Update("Nome novo", 300, false, DentistServiceStatus.indisponivel);

        // Assert
        service.Name.Should().Be("Nome novo");
        service.Price.Should().Be(300);
        service.Isperiodic.Should().BeFalse();
        service.Status.Should().Be(DentistServiceStatus.indisponivel);
    }

    [Fact]
    public void Update_WhenCalled_ShouldRefreshUpdatedAt()
    {
        // Arrange
        var service = DentistServicesFactory.Create();
        var beforeUpdate = service.UpdatedAt;

        // Act
        service.Update("Novo nome", 200, false, DentistServiceStatus.emBreve);

        // Assert
        service.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void Update_WhenCalled_ShouldPreserveIdAndDentistIdAndCreatedAt()
    {
        // Arrange
        var service   = DentistServicesFactory.Create();
        var originalId        = service.Id;
        var originalDentistId = service.DentistId;
        var originalCreatedAt = service.CreatedAt;

        // Act
        service.Update("Outro nome", 500, true, DentistServiceStatus.disponivel);

        // Assert
        service.Id.Should().Be(originalId);
        service.DentistId.Should().Be(originalDentistId);
        service.CreatedAt.Should().Be(originalCreatedAt);
    }

    // ──────────────────────────────────────────
    // IsOwnedBy
    // ──────────────────────────────────────────

    [Fact]
    public void IsOwnedBy_WhenDentistIdMatches_ShouldReturnTrue()
    {
        // Arrange
        var service = DentistServicesFactory.Create(dentistId: DentistServicesFactory.DefaultDentistId);

        // Act
        var result = service.IsOwnedBy(DentistServicesFactory.DefaultDentistId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsOwnedBy_WhenDentistIdDoesNotMatch_ShouldReturnFalse()
    {
        // Arrange
        var service = DentistServicesFactory.Create(dentistId: DentistServicesFactory.DefaultDentistId);

        // Act
        var result = service.IsOwnedBy(DentistServicesFactory.OtherDentistId);

        // Assert
        result.Should().BeFalse();
    }

    // ──────────────────────────────────────────
    // MarkAsDeleted
    // ──────────────────────────────────────────

    [Fact]
    public void MarkAsDeleted_WhenCalled_ShouldSetDeletedAtToUtcNow()
    {
        // Arrange
        var service = DentistServicesFactory.Create();

        // Act
        service.MarkAsDeleted();

        // Assert
        service.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MarkAsDeleted_WhenCalled_ShouldNotChangeOtherProperties()
    {
        // Arrange
        var service          = DentistServicesFactory.Create();
        var originalName     = service.Name;
        var originalPrice    = service.Price;
        var originalDentistId = service.DentistId;

        // Act
        service.MarkAsDeleted();

        // Assert
        service.Name.Should().Be(originalName);
        service.Price.Should().Be(originalPrice);
        service.DentistId.Should().Be(originalDentistId);
    }
}
