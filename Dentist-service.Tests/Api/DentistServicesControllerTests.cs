using Dentist_service.DentistsServices.Api.Controllers;
using Dentist_service.DentistsServices.Application.DTOs;
using Dentist_service.DentistsServices.Domain;
using Dentist_service.DentistsServices.Domain.Exceptions;
using Dentist_service.DentistsServices.Domain.Ports;
using Dentist_service.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Dentist_service.Tests.Api;

/// <summary>
/// Testes unitários para DentistServicesController.
/// O use case é mockado com Moq; o HttpContext é configurado para simular headers.
/// </summary>
public class DentistServicesControllerTests
{
    private readonly Mock<IDentistServicesUseCase>  _useCaseMock;
    private readonly DentistServicesController      _controller;

    public DentistServicesControllerTests()
    {
        _useCaseMock = new Mock<IDentistServicesUseCase>();
        _controller  = new DentistServicesController(_useCaseMock.Object);
    }

    // ──────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────

    private void SetDentistIdHeader(Guid dentistId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Dentist-Id"] = dentistId.ToString();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private void SetNoHeader()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static DentistServicesResponse BuildResponse(
        Guid?   id        = null,
        Guid?   dentistId = null,
        string  name      = "Limpeza dental") =>
        new(
            Id:        id        ?? DentistServicesFactory.DefaultId,
            Name:      name,
            DentistId: dentistId ?? DentistServicesFactory.DefaultDentistId,
            Price:     150,
            IsPeriodic: true,
            Status:    DentistServiceStatus.disponivel,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);

    // ──────────────────────────────────────────
    // GET /api/dentist-services
    // ──────────────────────────────────────────

    [Fact]
    public async Task GetAll_WhenHeaderIsPresent_ShouldReturn200WithList()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;
        var responses = new List<DentistServicesResponse> { BuildResponse(dentistId: dentistId) };

        SetDentistIdHeader(dentistId);
        _useCaseMock
            .Setup(u => u.GetAllByDentistAsync(dentistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responses);

        // Act
        var result = await _controller.GetAll(CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(responses);
    }

    [Fact]
    public async Task GetAll_WhenHeaderIsMissing_ShouldReturn400()
    {
        // Arrange
        SetNoHeader();

        // Act
        var result = await _controller.GetAll(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    // ──────────────────────────────────────────
    // GET /api/dentist-services/{id}
    // ──────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenServiceExistsAndOwnerMatches_ShouldReturn200()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;
        var response  = BuildResponse(dentistId: dentistId);

        SetDentistIdHeader(dentistId);
        _useCaseMock
            .Setup(u => u.GetByIdAsync(response.Id, dentistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetById(response.Id, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GetById_WhenServiceNotFound_ShouldReturn404()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;

        SetDentistIdHeader(dentistId);
        _useCaseMock
            .Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), dentistId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Serviço não encontrado."));

        // Act
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetById_WhenServiceBelongsToAnotherDentist_ShouldReturn403()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;

        SetDentistIdHeader(dentistId);
        _useCaseMock
            .Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), dentistId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Acesso negado."));

        // Act
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetById_WhenHeaderIsMissing_ShouldReturn400()
    {
        // Arrange
        SetNoHeader();

        // Act
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    // ──────────────────────────────────────────
    // POST /api/dentist-services
    // ──────────────────────────────────────────

    [Fact]
    public async Task Create_WhenRequestIsValid_ShouldReturn201WithLocation()
    {
        // Arrange
        var request = new CreateDentistServicesRequest(
            Name:       "Limpeza dental",
            DentistId:  DentistServicesFactory.DefaultDentistId,
            Price:      150,
            IsPeriodic: true,
            Status:     DentistServiceStatus.disponivel);

        var response = BuildResponse();

        _useCaseMock
            .Setup(u => u.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task Create_WhenValidationFails_ShouldReturn400()
    {
        // Arrange
        var request = new CreateDentistServicesRequest("", Guid.Empty, 0, false, DentistServiceStatus.disponivel);

        _useCaseMock
            .Setup(u => u.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("O nome do serviço é obrigatório."));

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    // ──────────────────────────────────────────
    // PUT /api/dentist-services/{id}
    // ──────────────────────────────────────────

    [Fact]
    public async Task Update_WhenServiceExistsAndDataIsValid_ShouldReturn200()
    {
        // Arrange
        var serviceId = DentistServicesFactory.DefaultId;
        var request   = new UpdateDentistServicesRequest("Clareamento", 300, false, DentistServiceStatus.emBreve);
        var response  = BuildResponse(id: serviceId, name: "Clareamento");

        _useCaseMock
            .Setup(u => u.UpdateAsync(serviceId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.Update(serviceId, request, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task Update_WhenServiceNotFound_ShouldReturn404()
    {
        // Arrange
        var request = new UpdateDentistServicesRequest("Nome", 100, true, DentistServiceStatus.disponivel);

        _useCaseMock
            .Setup(u => u.UpdateAsync(It.IsAny<Guid>(), request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Serviço não encontrado."));

        // Act
        var result = await _controller.Update(Guid.NewGuid(), request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(404);
    }

    // ──────────────────────────────────────────
    // DELETE /api/dentist-services/{id}
    // ──────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenServiceExistsAndOwnerMatches_ShouldReturn204()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;
        var serviceId = DentistServicesFactory.DefaultId;

        SetDentistIdHeader(dentistId);
        _useCaseMock
            .Setup(u => u.DeleteAsync(serviceId, dentistId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(serviceId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>()
            .Which.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task Delete_WhenServiceNotFound_ShouldReturn404()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;

        SetDentistIdHeader(dentistId);
        _useCaseMock
            .Setup(u => u.DeleteAsync(It.IsAny<Guid>(), dentistId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Serviço não encontrado."));

        // Act
        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Delete_WhenServiceBelongsToAnotherDentist_ShouldReturn403()
    {
        // Arrange
        var dentistId = DentistServicesFactory.DefaultDentistId;

        SetDentistIdHeader(dentistId);
        _useCaseMock
            .Setup(u => u.DeleteAsync(It.IsAny<Guid>(), dentistId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Acesso negado."));

        // Act
        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Delete_WhenHeaderIsMissing_ShouldReturn400()
    {
        // Arrange
        SetNoHeader();

        // Act
        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }
}
