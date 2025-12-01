using HRM.Shared.Kernel.Interfaces;

namespace HRM.Shared.IntegrationEvents
{
    public class UserCreatedIntegrationEvent : IIntegrationEvent
    {
        public Guid UserId { get; }
        public string Email { get; }

        public UserCreatedIntegrationEvent(Guid userId, string email)
        {
            UserId = userId;
            Email = email;
        }
    }
}
