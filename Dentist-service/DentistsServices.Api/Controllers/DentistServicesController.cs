using Dentist_service.DentistsServices.Application.DTOs;
using Dentist_service.DentistsServices.Domain.Exceptions;
using Dentist_service.DentistsServices.Domain.Ports;
using Microsoft.AspNetCore.Mvc;

namespace Dentist_service.DentistsServices.Api.Controllers;

/// <summary>
/// Adaptador de entrada (HTTP): expõe os casos de uso de DentistServices via REST.
/// O dentistId do requisitante é lido do header X-Dentist-Id.
/// Quando JWT for adicionado, substituir pela leitura do claim correspondente.
/// </summary>
[ApiController]
[Route("api/dentist-services")]
[Produces("application/json")]
public class DentistServicesController : ControllerBase
{
    private readonly IDentistServicesUseCase _useCase;

    public DentistServicesController(IDentistServicesUseCase useCase)
    {
        _useCase = useCase;
    }

    // ──────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────

    /// <summary>
    /// Lê o dentistId do header X-Dentist-Id.
    /// Retorna null se o header estiver ausente ou inválido.
    /// </summary>
    private Guid? GetRequestingDentistId()
    {
        if (Request.Headers.TryGetValue("X-Dentist-Id", out var value) &&
            Guid.TryParse(value, out var dentistId))
        {
            return dentistId;
        }
        return null;
    }

    // ──────────────────────────────────────────
    // GET /api/dentist-services
    // ──────────────────────────────────────────

    /// <summary>
    /// Lista todos os serviços do dentista autenticado (X-Dentist-Id).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DentistServicesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var dentistId = GetRequestingDentistId();
        if (dentistId is null)
            return BadRequest(new { message = "Header 'X-Dentist-Id' ausente ou inválido." });

        var result = await _useCase.GetAllByDentistAsync(dentistId.Value, cancellationToken);
        return Ok(result);
    }

    // ──────────────────────────────────────────
    // GET /api/dentist-services/{id}
    // ──────────────────────────────────────────

    /// <summary>
    /// Retorna um serviço pelo Id, desde que pertença ao dentista autenticado.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DentistServicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dentistId = GetRequestingDentistId();
        if (dentistId is null)
            return BadRequest(new { message = "Header 'X-Dentist-Id' ausente ou inválido." });

        try
        {
            var result = await _useCase.GetByIdAsync(id, dentistId.Value, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DomainException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    // ──────────────────────────────────────────
    // POST /api/dentist-services
    // ──────────────────────────────────────────

    /// <summary>Cria um novo serviço odontológico.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DentistServicesResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDentistServicesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _useCase.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ──────────────────────────────────────────
    // PUT /api/dentist-services/{id}
    // ──────────────────────────────────────────

    /// <summary>Atualiza um serviço odontológico existente.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DentistServicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDentistServicesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _useCase.UpdateAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ──────────────────────────────────────────
    // DELETE /api/dentist-services/{id}
    // ──────────────────────────────────────────

    /// <summary>
    /// Remove um serviço, desde que pertença ao dentista autenticado.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var dentistId = GetRequestingDentistId();
        if (dentistId is null)
            return BadRequest(new { message = "Header 'X-Dentist-Id' ausente ou inválido." });

        try
        {
            await _useCase.DeleteAsync(id, dentistId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DomainException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }
}
