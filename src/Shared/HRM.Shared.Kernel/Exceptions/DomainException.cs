namespace HRM.Shared.Kernel.Exceptions
{
    public abstract class DomainException : Exception
    {
        protected DomainException(string message) : base(message)
        {
        }
    }

    public class NotFoundException : DomainException
    {
        public NotFoundException(string message) : base(message)
        {
        }
    }

    public class ValidationException : DomainException
    {
        public IEnumerable<string> Errors { get; }

        public ValidationException(IEnumerable<string> errors) : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }
    }
}
