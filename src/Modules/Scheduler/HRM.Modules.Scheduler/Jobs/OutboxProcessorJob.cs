using HRM.Shared.Kernel.Domain;
using HRM.Shared.Kernel.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Quartz;

namespace HRM.Modules.Scheduler.Jobs
{
    [DisallowConcurrentExecution]
    public class OutboxProcessorJob : IJob
    {
        private readonly IEnumerable<IModuleDbContext> _moduleDbContexts;
        private readonly IPublisher _publisher;

        public OutboxProcessorJob(IEnumerable<IModuleDbContext> moduleDbContexts, IPublisher publisher)
        {
            _moduleDbContexts = moduleDbContexts;
            _publisher = publisher;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            foreach (var dbContext in _moduleDbContexts)
            {
                var messages = await dbContext.OutboxMessages
                    .Where(m => m.ProcessedDateUtc == null)
                    .OrderBy(m => m.OccurredOnUtc)
                    .Take(20)
                    .ToListAsync(context.CancellationToken);

                if (!messages.Any())
                {
                    continue;
                }

                foreach (var message in messages)
                {
                    var eventType = Type.GetType(message.Type);
                    if (eventType is null)
                    {
                        // Log a warning: could not find type
                        continue;
                    }

                    var domainEvent = JsonConvert.DeserializeObject(message.Data, eventType) as IDomainEvent;
                    if (domainEvent is null)
                    {
                        // Log a warning: could not deserialize
                        continue;
                    }

                    await _publisher.Publish(domainEvent, context.CancellationToken);

                    message.MarkAsProcessed(DateTime.UtcNow);
                }

                await dbContext.SaveChangesAsync(context.CancellationToken);
            }
        }
    }
}
