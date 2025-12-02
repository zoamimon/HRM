namespace HRM.Modules.Personnel.Domain.Entities
{
    public class EmployeeCompanyAssignment
    {
        public Guid Id { get; private set; }
        public Guid EmployeeId { get; private set; }
        public Guid CompanyId { get; private set; }
        public Guid DepartmentId { get; private set; }
        public Guid PositionId { get; private set; }
        public bool IsPrimaryRole { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime? EndDate { get; private set; }

        public virtual Employee Employee { get; private set; }

        private EmployeeCompanyAssignment() { }

        public EmployeeCompanyAssignment(Guid employeeId, Guid companyId, Guid departmentId, Guid positionId, bool isPrimary, DateTime startDate)
        {
            Id = Guid.NewGuid();
            EmployeeId = employeeId;
            CompanyId = companyId;
            DepartmentId = departmentId;
            PositionId = positionId;
            IsPrimaryRole = isPrimary;
            StartDate = startDate;
        }

        public void EndAssignment(DateTime endDate)
        {
            if (endDate > StartDate)
            {
                EndDate = endDate;
            }
        }

        public void SetAsNonPrimary()
        {
            IsPrimaryRole = false;
        }
    }
}
