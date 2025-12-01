using MediatR;

namespace HRM.Shared.Kernel.Messaging
{
    public interface IMessageBroker
    {
        Task PublishAsync(INotification notification);
    }

    public class InMemoryMessageBroker : IMessageBroker
    {
        private readonly IPublisher _publisher;

        public InMemoryMessageBroker(IPublisher publisher)
        {
            _publisher = publisher;
        }

        public Task PublishAsync(INotification notification)
        {
            return _publisher.Publish(notification);
        }
    }
}
