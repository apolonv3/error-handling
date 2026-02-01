using ErrorHandling.Domain.Exceptions;
using ErrorHandling.Domain.Results;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Domain.Entities;

public class Customer
{
    public Guid Id { get; init; }
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public Money CreditLimit { get; private set; }
    public Money AvailableCredit { get; private set; }
    public CustomerStatus Status { get; private set; }
    public DateTime CreatedAt { get; init; }
    public int Version { get; private set; }

    private Customer()
    {
        Name = null!;
        Email = null!;
        CreditLimit = null!;
        AvailableCredit = null!;
    }

    // Exception-based constructor
    public Customer(string name, Email email, Money creditLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("name", "Customer name is required");

        if (email == null)
            throw new ArgumentNullException(nameof(email));

        if (creditLimit is null)
            throw new ArgumentNullException(nameof(creditLimit));

        Id = Guid.NewGuid();
        Name = name;
        Email = email;
        CreditLimit = creditLimit;
        AvailableCredit = creditLimit;
        Status = CustomerStatus.Active;
        CreatedAt = DateTime.UtcNow;
        Version = 0;
    }

    // Result-based factory
    public static Result<Customer> Create(
        string name,
        string emailValue,
        decimal creditLimit,
        string currency = "USD"
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Customer>.Failure(Error.Validation("name", "Customer name is required"));

        var emailResult = Email.TryCreate(emailValue);
        if (emailResult.IsFailure)
            return Result<Customer>.Failure(emailResult.Error!);

        var creditLimitResult = Money.TryCreate(creditLimit, currency);
        if (creditLimitResult.IsFailure)
            return Result<Customer>.Failure(creditLimitResult.Error!);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = emailResult.Value,
            CreditLimit = creditLimitResult.Value,
            AvailableCredit = creditLimitResult.Value,
            Status = CustomerStatus.Active,
            CreatedAt = DateTime.UtcNow,
            Version = 0,
        };

        return Result<Customer>.Success(customer);
    }

    // Exception-based method
    public void UseCredit(Money amount)
    {
        if (amount is null)
            throw new ArgumentNullException(nameof(amount));

        if (Status != CustomerStatus.Active)
            throw new InvalidStateTransitionException(
                Status.ToString(),
                "UsingCredit",
                nameof(Customer)
            );

        if (AvailableCredit < amount)
            throw new BusinessRuleException(
                "INSUFFICIENT_CREDIT",
                $"Available credit ({AvailableCredit}) is less than requested amount ({amount})"
            );

        AvailableCredit = AvailableCredit.Subtract(amount);
    }

    // Result-based method
    public Result<Money> TryUseCredit(Money amount)
    {
        if (amount is null)
            return Result<Money>.Failure(new NullValueError("amount", "Amount cannot be null"));

        if (Status != CustomerStatus.Active)
            return Result<Money>.Failure(new CustomerNotActiveError(Id, Status));

        var subtractResult = AvailableCredit.TrySubtract(amount);
        if (subtractResult.IsFailure)
            return Result<Money>.Failure(new InsufficientCreditError(amount, AvailableCredit));

        AvailableCredit = subtractResult.Value;
        return Result<Money>.Success(AvailableCredit);
    }

    public void RestoreCredit(Money amount)
    {
        if (amount is null)
            throw new ArgumentNullException(nameof(amount));

        var newCredit = AvailableCredit.Add(amount);
        if (newCredit > CreditLimit)
            throw new BusinessRuleException(
                "CREDIT_OVERFLOW",
                $"Restoring {amount} would exceed credit limit of {CreditLimit}"
            );

        AvailableCredit = newCredit;
    }

    public Result RestoreCreditSafe(Money amount)
    {
        if (amount is null)
            return Result.Failure(new NullValueError("amount", "Amount cannot be null"));

        var addResult = AvailableCredit.TryAdd(amount);
        if (addResult.IsFailure)
            return Result.Failure(addResult.Error!);

        if (addResult.Value > CreditLimit)
            return Result.Failure(new CreditOverflowError(amount, CreditLimit));

        AvailableCredit = addResult.Value;
        return Result.Success();
    }

    public void Suspend(string reason)
    {
        if (Status == CustomerStatus.Suspended)
            throw new InvalidStateTransitionException(
                Status.ToString(),
                CustomerStatus.Suspended.ToString(),
                nameof(Customer)
            );

        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("reason", "Suspension reason is required");

        Status = CustomerStatus.Suspended;
        Version++;
    }

    public Result Activate()
    {
        if (Status == CustomerStatus.Active)
            return Result.Failure("ALREADY_ACTIVE", "Customer is already active");

        Status = CustomerStatus.Active;
        Version++;
        return Result.Success();
    }

    public void UpdateVersion(int expectedVersion)
    {
        if (Version != expectedVersion)
            throw new OptimisticLockException(
                nameof(Customer),
                Id.ToString(),
                expectedVersion,
                Version
            );

        Version++;
    }
}

public enum CustomerStatus
{
    Active,
    Suspended,
    Closed,
}
