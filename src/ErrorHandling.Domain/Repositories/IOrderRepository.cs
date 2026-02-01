using ErrorHandling.Domain.Entities;

namespace ErrorHandling.Domain.Repositories;

/// <summary>
/// Repository contract for the <see cref="Order"/> aggregate.
/// </summary>
/// <remarks>
/// Abstracts persistence of orders. Used by order services to load an order by ID
/// and to save order state after create, add item, submit, payment, ship, or cancel.
/// </remarks>
public interface IOrderRepository
{
    /// <summary>Gets an order by ID, or null if not found.</summary>
    Task<Order?> GetByIdAsync(Guid id);

    /// <summary>Persists an order (insert or update).</summary>
    Task SaveAsync(Order order);
}
