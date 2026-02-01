using ErrorHandling.Domain.Entities;

namespace ErrorHandling.Domain.Repositories;

/// <summary>
/// Repository contract for <see cref="Order"/> aggregate.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
    Task SaveAsync(Order order);
}
