namespace HRM.Shared.Kernel.Interfaces
{
    public interface IIntegrationEventPublisher
    {
        Task PublishAsync(IIntegrationEvent integrationEvent);
    }
}
