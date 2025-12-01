using HRM.Modules.Identity.Application.DAL;
using HRM.Modules.Identity.Application.Services;
using HRM.Modules.Identity.Domain.Entities;
using HRM.Shared.IntegrationEvents;
using HRM.Shared.Kernel.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Auth.Commands
{
    public class SignUpCommandHandler : IRequestHandler<SignUpCommand, Guid>
    {
        private readonly IIdentityDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IIntegrationEventPublisher _eventPublisher;

        public SignUpCommandHandler(IIdentityDbContext context, IPasswordHasher passwordHasher, IIntegrationEventPublisher eventPublisher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _eventPublisher = eventPublisher;
        }

        public async Task<Guid> Handle(SignUpCommand request, CancellationToken cancellationToken)
        {
            var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
            if (emailExists)
            {
                throw new Exception("Email already exists."); // Replace with custom exception later
            }

            var hashedPassword = _passwordHasher.HashPassword(request.Password);
            var user = new User(Guid.NewGuid(), request.Email, hashedPassword);

            await _context.Users.AddAsync(user, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Publish integration event
            var integrationEvent = new UserCreatedIntegrationEvent(user.UserId, user.Email);
            await _eventPublisher.PublishAsync(integrationEvent);

            return user.UserId;
        }
    }
}
