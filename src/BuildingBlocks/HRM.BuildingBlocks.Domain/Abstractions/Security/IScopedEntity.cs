namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// Interface for entities that support data scoping
/// Entities implementing this can be filtered by EfScopeExpressionBuilder
///
/// Usage:
/// <code>
/// public class Employee : Entity, IScopedEntity
/// {
///     public Guid? CompanyId { get; private set; }
///     public Guid? DepartmentId { get; private set; }
///     public Guid? PositionId { get; private set; }
///     public Guid OwnerId => Id; // Employee owns their own data
/// }
/// </code>
/// </summary>
public interface IScopedEntity
{
    /// <summary>
    /// Entity's primary key
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Company this entity belongs to (for company scope)
    /// Null if not applicable
    /// </summary>
    Guid? CompanyId { get; }

    /// <summary>
    /// Department this entity belongs to (for department scope)
    /// Null if not applicable
    /// </summary>
    Guid? DepartmentId { get; }

    /// <summary>
    /// Position this entity belongs to (for position scope)
    /// Null if not applicable
    /// </summary>
    Guid? PositionId { get; }

    /// <summary>
    /// Owner of this entity (for self scope)
    /// Returns the user/employee ID who owns this data
    /// </summary>
    Guid OwnerId { get; }
}

/// <summary>
/// Base interface for simpler scoped entities that only need company scope
/// </summary>
public interface ICompanyScopedEntity
{
    Guid Id { get; }
    Guid? CompanyId { get; }
}

/// <summary>
/// Interface for entities owned by a specific user
/// </summary>
public interface IOwnedEntity
{
    Guid Id { get; }
    Guid OwnerId { get; }
}
