using MediatR;

namespace HRM.Modules.Personnel.Application.Features.Employees.Queries
{
    public class GetEmployeeByIdQuery : IRequest<EmployeeDto>
    {
        public Guid EmployeeId { get; set; }
    }

    public class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDto>
    {
        private readonly DAL.IPersonnelDbContext _context;

        public GetEmployeeByIdQueryHandler(DAL.IPersonnelDbContext context)
        {
            _context = context;
        }

        public async Task<EmployeeDto> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
        {
            var employee = await _context.Employees
                .FindAsync(new object[] { request.EmployeeId }, cancellationToken);

            if (employee == null)
            {
                return null;
            }

            return new EmployeeDto(employee.EmployeeId, employee.FirstName, employee.LastName, employee.Email);
        }
    }
}
