using ErrorHandling.Domain.Entities;

namespace ErrorHandling.Domain.Repositories;

/// <summary>
/// Repository contract for the <see cref="Customer"/> aggregate.
/// </summary>
/// <remarks>
/// Abstracts persistence of customers. Implementations can be in-memory (demo),
/// Entity Framework, or any other data store. The domain and application layers
/// depend only on this interface, not on concrete storage.
/// </remarks>
public interface ICustomerRepository
{
    /// <summary>Gets a customer by ID, or null if not found.</summary>
    Task<Customer?> GetByIdAsync(Guid id);

    /// <summary>Gets a customer by ID, or null if not found. Alias for GetByIdAsync for semantic clarity.</summary>
    Task<Customer?> GetByIdOrDefaultAsync(Guid id);

    /// <summary>Persists a customer (insert or update).</summary>
    Task SaveAsync(Customer customer);
}
