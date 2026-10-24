using Employee_service.Employees.Application.DTOs;
using Employee_service.Employees.Application.Services;
using Employee_service.Employees.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Employee_service.Employees.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Produces("application/json")]
public sealed class AppointmentsController(IAppointmentService appointmentService) : ControllerBase
{
    /// <summary>Solicita ao Dentist Service a alteração de um agendamento.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await appointmentService.UpdateAsync(id, request, cancellationToken));
        }
        catch (DentistServiceException ex)
        {
            return new ContentResult
            {
                StatusCode = (int)ex.StatusCode,
                ContentType = "application/json",
                Content = ex.ResponseBody
            };
        }
        catch (DentistServiceUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Dentist Service indisponível." });
        }
    }
}
