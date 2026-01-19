using HRM.BuildingBlocks.Application.Abstractions.Authentication;
using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.Modules.Identity.Domain.Entities;
using HRM.Modules.Identity.Domain.Errors;
using HRM.Modules.Identity.Domain.Repositories;

namespace HRM.Modules.Identity.Application.Commands.RegisterOperator;

/// <summary>
/// Handler for RegisterOperatorCommand
/// Creates new operator in Pending status
///
/// Dependencies:
/// - IOperatorRepository: Check uniqueness and persist operator
/// - IPasswordHasher: Hash password with BCrypt
/// - IUnitOfWork: Commit transaction (injected via UnitOfWorkBehavior)
///
/// Processing Steps:
/// 1. Check username uniqueness (409 Conflict if exists)
/// 2. Check email uniqueness (409 Conflict if exists)
/// 3. Hash password with BCrypt (cost factor 11)
/// 4. Create Operator aggregate via factory method
/// 5. Add to repository (EF tracks entity)
/// 6. Domain event dispatched automatically (OperatorRegisteredDomainEvent)
/// 7. Return operator ID
///
/// Error Handling:
/// - Result pattern: Success<Guid> or Failure<Error>
/// - Validation errors: Handled by FluentValidation (ValidationBehavior)
/// - Business rule violations: Returned as domain errors
/// - Infrastructure errors: Propagated as exceptions (logged by LoggingBehavior)
///
/// Transaction Management:
/// - UnitOfWorkBehavior wraps handler in transaction
/// - Domain events dispatched BEFORE SaveChangesAsync
/// - Integration events created in OutboxMessages table (same transaction)
/// - Rollback on any error
///
/// Performance:
/// - 2 database queries (username/email uniqueness check)
/// - 1 BCrypt hash operation (~100ms)
/// - 1 INSERT operation
/// - Total: ~150-200ms
/// </summary>
internal sealed class RegisterOperatorCommandHandler : ICommandHandler<RegisterOperatorCommand, Guid>
{
    private readonly IOperatorRepository _operatorRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterOperatorCommandHandler(
        IOperatorRepository operatorRepository,
        IPasswordHasher passwordHasher)
    {
        _operatorRepository = operatorRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<Guid>> Handle(RegisterOperatorCommand request, CancellationToken cancellationToken)
    {
        // 1. Check username uniqueness
        if (await _operatorRepository.ExistsByUsernameAsync(request.Username, cancellationToken))
        {
            return Result.Failure<Guid>(OperatorErrors.UsernameAlreadyExists(request.Username));
        }

        // 2. Check email uniqueness
        if (await _operatorRepository.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            return Result.Failure<Guid>(OperatorErrors.EmailAlreadyExists(request.Email));
        }

        // 3. Hash password with BCrypt
        // Cost factor 11 (2^11 = 2048 rounds) - balances security vs performance
        // Takes ~100ms on modern hardware
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 4. Create Operator aggregate
        // Factory method encapsulates creation logic
        // Sets status to Pending (must be activated by admin)
        // Raises OperatorRegisteredDomainEvent
        var @operator = Operator.Register(
            username: request.Username,
            email: request.Email,
            passwordHash: passwordHash,
            fullName: request.FullName,
            phoneNumber: request.PhoneNumber
        );

        // 5. Add to repository
        // EF Core tracks entity (no explicit SaveChanges yet)
        _operatorRepository.Add(@operator);

        // 6. Unit of Work commits transaction
        // UnitOfWorkBehavior calls SaveChangesAsync after handler returns
        // Domain events dispatched during SaveChanges
        // Integration events created in OutboxMessages table

        // 7. Return operator ID
        return Result.Success(@operator.Id);
    }
}
