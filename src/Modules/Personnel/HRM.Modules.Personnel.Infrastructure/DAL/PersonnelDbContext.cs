using HRM.Modules.Personnel.Application.DAL;
using HRM.Modules.Personnel.Domain.Entities;
using HRM.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace HRM.Modules.Personnel.Infrastructure.DAL
{
    public class PersonnelDbContext : DbContext, IPersonnelDbContext
    {
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmployeeCompanyAssignment> EmployeeCompanyAssignments { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; } // Now satisfies IModuleDbContext

        public PersonnelDbContext(DbContextOptions<PersonnelDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>(builder =>
            {
                builder.HasKey(e => e.EmployeeId);
                builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                builder.Property(e => e.Email).IsRequired().HasMaxLength(256);
                builder.HasIndex(e => e.Email).IsUnique();

                builder.HasMany(e => e.Assignments)
                    .WithOne(a => a.Employee)
                    .HasForeignKey(a => a.EmployeeId);

                builder.Ignore(e => e.DomainEvents);
            });

            modelBuilder.Entity<EmployeeCompanyAssignment>(builder =>
            {
                builder.HasKey(a => a.Id);
                // Note: EF Core can't enforce relationships across module boundaries (bounded contexts).
                // This will need to be validated in the Application layer.
                builder.Property(a => a.CompanyId).IsRequired();
                builder.Property(a => a.DepartmentId).IsRequired();
                builder.Property(a => a.PositionId).IsRequired();
            });

            modelBuilder.Entity<OutboxMessage>(builder =>
            {
                builder.HasKey(om => om.Id);
                builder.Property(om => om.Data).IsRequired();
                builder.Property(om => om.Type).IsRequired();
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ConvertDomainEventsToOutboxMessages();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void ConvertDomainEventsToOutboxMessages()
        {
            var outboxMessages = ChangeTracker
                .Entries<AggregateRoot>()
                .Select(x => x.Entity)
                .SelectMany(aggregateRoot =>
                {
                    var domainEvents = aggregateRoot.DomainEvents.ToList();
                    aggregateRoot.ClearDomainEvents();
                    return domainEvents;
                })
                .Select(domainEvent => new OutboxMessage(
                    domainEvent.OccurredOn,
                    domainEvent.GetType().AssemblyQualifiedName,
                    JsonConvert.SerializeObject(domainEvent)))
                .ToList();

            OutboxMessages.AddRange(outboxMessages);
        }
    }
}
