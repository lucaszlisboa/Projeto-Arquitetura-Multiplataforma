using Employee_service.Employees.Application.DTOs;

namespace Employee_service.Employees.Application.Services;

public interface IAppointmentService
{
    Task<AppointmentResponse> UpdateAsync(Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken);
}
