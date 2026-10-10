using Dentist_service.DentistsServices.Application.DTOs;
using Dentist_service.DentistsServices.Application.UseCases;
using Dentist_service.DentistsServices.Domain;
using Dentist_service.DentistsServices.Domain.Exceptions;
using Dentist_service.DentistsServices.Domain.Ports;
using Dentist_service.Tests.Helpers;
using FluentAssertions;
using Moq;

namespace Dentist_service.Tests.Application;

/// <summary>
/// Testes unitários para DentistServicesUseCase.
/// As dependências (repositório e validator) são mockadas com Moq.
/// </summary>
public class DentistServicesUseCaseTests
{
    private readonly Mock<IDentistServicesRepository> _repositoryMock;
    private readonly Mock<IDentistServicesValidator>  _validatorMock;
    private readonly DentistServicesUseCase           _useCase;

    public DentistServicesUseCaseTests()
    {
        _repositoryMock = new Mock<IDentistServicesRepository>();
        _validatorMock  = new Mock<IDentistServicesValidator>();
        _useCase        = new DentistServicesUseCase(_repositoryMock.Object, _validatorMock.Object);
    }

    // ──────────────────────────────────────────
    // GetByIdAsync
    // ──────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenServiceExistsAndOwnerMatches_ShouldReturnResponse()
    {
        // Arrange
        var service   = DentistServicesFactory.Create();
        var dentistId = DentistServicesFactory.DefaultDentistId;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(service.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        // Act
        var result = await _useCase.GetByIdAsync(service.Id, dentistId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(service.Id);
        result.Name.Should().Be(service.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenServiceNotFound_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DentistServices?)null);

        // Act
        var act = async () => await _useCase.GetByIdAsync(Guid.NewGuid(), dentistId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenServiceBelongsToAnotherDentist_ShouldThrowDomainException()
    {
        // Arrange
        var service         = DentistServicesFactory.Create(dentistId: DentistServicesFactory.DefaultDentistId);
        var anotherDentistId = DentistServicesFactory.OtherDentistId;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(service.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        // Act
        var act = async () => await _useCase.GetByIdAsync(service.Id, anotherDentistId);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Acesso negado*");
    }

    // ──────────────────────────────────────────
    // GetAllByDentistAsync
    // ──────────────────────────────────────────

    [Fact]
    public async Task GetAllByDentistAsync_WhenDentistHasServices_ShouldReturnOnlyHisServices()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;
        var services  = new List<DentistServices>
        {
            DentistServicesFactory.Create(id: Guid.NewGuid(), dentistId: dentistId, name: "Serviço 1"),
            DentistServicesFactory.Create(id: Guid.NewGuid(), dentistId: dentistId, name: "Serviço 2"),
        };

        _repositoryMock
            .Setup(r => r.GetByDentistIdAsync(dentistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var result = await _useCase.GetAllByDentistAsync(dentistId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.DentistId == dentistId);
    }

    [Fact]
    public async Task GetAllByDentistAsync_WhenDentistHasNoServices_ShouldReturnEmptyList()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;

        _repositoryMock
            .Setup(r => r.GetByDentistIdAsync(dentistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DentistServices>());

        // Act
        var result = await _useCase.GetAllByDentistAsync(dentistId);

        // Assert
        result.Should().BeEmpty();
    }

    // ──────────────────────────────────────────
    // CreateAsync
    // ──────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_ShouldPersistAndReturnResponse()
    {
        // Arrange
        var request = new CreateDentistServicesRequest(
            Name:       "Limpeza dental",
            DentistId:  DentistServicesFactory.DefaultDentistId,
            Price:      150,
            IsPeriodic: true,
            Status:     DentistServiceStatus.disponivel);

        _validatorMock
            .Setup(v => v.Validate(It.IsAny<DentistServices>()))
            .Returns(new Dentist_service.DentistsServices.Domain.ValidationResult());

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<DentistServices>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _useCase.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be(request.Name);
        result.DentistId.Should().Be(request.DentistId);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<DentistServices>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenValidationFails_ShouldThrowArgumentException()
    {
        // Arrange
        var request = new CreateDentistServicesRequest(
            Name:       "",
            DentistId:  DentistServicesFactory.DefaultDentistId,
            Price:      0,
            IsPeriodic: false,
            Status:     DentistServiceStatus.disponivel);

        var validationResult = new Dentist_service.DentistsServices.Domain.ValidationResult();
        validationResult.AddError("O nome do serviço é obrigatório.");
        validationResult.AddError("O preço deve ser maior que zero.");

        _validatorMock
            .Setup(v => v.Validate(It.IsAny<DentistServices>()))
            .Returns(validationResult);

        // Act
        var act = async () => await _useCase.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*nome*");

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<DentistServices>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────
    // UpdateAsync
    // ──────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WhenServiceExistsAndDataIsValid_ShouldUpdateAndReturnResponse()
    {
        // Arrange
        var service = DentistServicesFactory.Create();
        var request = new UpdateDentistServicesRequest(
            Name:       "Clareamento dental",
            Price:      350,
            IsPeriodic: false,
            Status:     DentistServiceStatus.emBreve);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(service.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        _validatorMock
            .Setup(v => v.Validate(It.IsAny<DentistServices>()))
            .Returns(new Dentist_service.DentistsServices.Domain.ValidationResult());

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<DentistServices>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _useCase.UpdateAsync(service.Id, request);

        // Assert
        result.Name.Should().Be("Clareamento dental");
        result.Price.Should().Be(350);
        result.Status.Should().Be(DentistServiceStatus.emBreve);

        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<DentistServices>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenServiceNotFound_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var request = new UpdateDentistServicesRequest("Nome", 100, true, DentistServiceStatus.disponivel);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DentistServices?)null);

        // Act
        var act = async () => await _useCase.UpdateAsync(Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ──────────────────────────────────────────
    // DeleteAsync
    // ──────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenServiceExistsAndOwnerMatches_ShouldCallRepositoryDelete()
    {
        // Arrange
        var service   = DentistServicesFactory.Create();
        var dentistId = DentistServicesFactory.DefaultDentistId;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(service.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        _repositoryMock
            .Setup(r => r.DeleteAsync(service.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _useCase.DeleteAsync(service.Id, dentistId);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(service.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenServiceNotFound_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DentistServices?)null);

        // Act
        var act = async () => await _useCase.DeleteAsync(Guid.NewGuid(), DentistServicesFactory.DefaultDentistId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenServiceBelongsToAnotherDentist_ShouldThrowDomainException()
    {
        // Arrange
        var service          = DentistServicesFactory.Create(dentistId: DentistServicesFactory.DefaultDentistId);
        var anotherDentistId = DentistServicesFactory.OtherDentistId;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(service.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        // Act
        var act = async () => await _useCase.DeleteAsync(service.Id, anotherDentistId);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Acesso negado*");

        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
