using ErrorHandling.Domain.Entities;

namespace ErrorHandling.Domain.Repositories;

/// <summary>
/// Repository contract for <see cref="Customer"/> aggregate.
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id);
    Task<Customer?> GetByIdOrDefaultAsync(Guid id);
    Task SaveAsync(Customer customer);
}
