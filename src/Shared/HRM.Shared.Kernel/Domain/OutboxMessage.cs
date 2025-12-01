namespace HRM.Shared.Kernel.Domain
{
    public class OutboxMessage
    {
        public Guid Id { get; private set; }
        public DateTime OccurredOnUtc { get; private set; }
        public string Type { get; private set; }
        public string Data { get; private set; }
        public DateTime? ProcessedDateUtc { get; private set; }

        private OutboxMessage() { }

        public OutboxMessage(DateTime occurredOnUtc, string type, string data)
        {
            Id = Guid.NewGuid();
            OccurredOnUtc = occurredOnUtc;
            Type = type;
            Data = data;
        }

        public void MarkAsProcessed(DateTime processedDateUtc)
        {
            ProcessedDateUtc = processedDateUtc;
        }
    }
}
