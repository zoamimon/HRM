using HRM.Modules.Personnel.Application.DAL;
using HRM.Modules.Personnel.Domain.Entities;
using HRM.Shared.IntegrationEvents;
using HRM.Shared.Kernel.Interfaces;

namespace HRM.Modules.Personnel.Application.Features.Employees.EventHandlers
{
    public class UserCreatedIntegrationEventHandler : IIntegrationEventHandler<UserCreatedIntegrationEvent>
    {
        private readonly IPersonnelDbContext _context;

        public UserCreatedIntegrationEventHandler(IPersonnelDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UserCreatedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            // A new user has been created in the Identity module; create a corresponding employee.
            // For now, we'll just use the email as the first and last name as a placeholder.
            var employee = new Employee(@event.UserId, @event.Email, @event.Email, @event.Email);

            await _context.Employees.AddAsync(employee, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
