using ErrorHandling.Domain.Exceptions;
using ErrorHandling.Domain.Results;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Domain.Entities;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Money Price { get; private set; }
    public int StockQuantity { get; private set; }
    public string Sku { get; private set; }
    public bool IsActive { get; private set; }

    private Product()
    {
        Name = null!;
        Description = null!;
        Price = null!;
        Sku = null!;
    }

    // Exception-based constructor
    public Product(string name, string description, Money? price, int stockQuantity, string sku)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("name", "Product name is required");

        if (price is null)
            throw new ArgumentNullException(nameof(price));

        if (price.Amount <= 0)
            throw new BusinessRuleException(
                "POSITIVE_PRICE",
                "Product price must be greater than zero"
            );

        if (stockQuantity < 0)
            throw new ValidationException("stockQuantity", "Stock quantity cannot be negative");

        if (string.IsNullOrWhiteSpace(sku))
            throw new ValidationException("sku", "SKU is required");

        Id = Guid.NewGuid();
        Name = name;
        Description = description ?? string.Empty;
        Price = price;
        StockQuantity = stockQuantity;
        Sku = sku.ToUpperInvariant();
        IsActive = true;
    }

    // Result-based factory
    public static Result<Product> Create(
        string name,
        string description,
        decimal price,
        string currency,
        int stockQuantity,
        string sku
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Product>.Failure(Error.Validation("name", "Product name is required"));

        if (string.IsNullOrWhiteSpace(sku))
            return Result<Product>.Failure(Error.Validation("sku", "SKU is required"));

        if (stockQuantity < 0)
            return Result<Product>.Failure(
                Error.Validation("stockQuantity", "Stock quantity cannot be negative")
            );

        var priceResult = Money.TryCreate(price, currency);
        if (priceResult.IsFailure)
            return Result<Product>.Failure(priceResult.Error!);

        if (priceResult.Value.Amount <= 0)
            return Result<Product>.Failure(
                new BusinessRuleError("POSITIVE_PRICE", "Product price must be greater than zero")
            );

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description ?? string.Empty,
            Price = priceResult.Value,
            StockQuantity = stockQuantity,
            Sku = sku.ToUpperInvariant(),
            IsActive = true,
        };

        return Result<Product>.Success(product);
    }

    // Exception-based method
    public void ReserveStock(int quantity)
    {
        if (quantity <= 0)
            throw new ValidationException("quantity", "Quantity must be greater than zero");

        if (!IsActive)
            throw new BusinessRuleException(
                "PRODUCT_INACTIVE",
                "Cannot reserve stock for inactive product"
            );

        if (StockQuantity < quantity)
            throw new BusinessRuleException(
                "INSUFFICIENT_STOCK",
                $"Insufficient stock. Available: {StockQuantity}, Requested: {quantity}"
            );

        StockQuantity -= quantity;
    }

    // Result-based method
    public Result<int> TryReserveStock(int quantity)
    {
        if (quantity <= 0)
            return Result<int>.Failure(
                new OrderValidationError("quantity", "Quantity must be greater than zero")
            );

        if (!IsActive)
            return Result<int>.Failure(new ProductInactiveError(Id, Name));

        if (StockQuantity < quantity)
            return Result<int>.Failure(new InsufficientStockError(StockQuantity, quantity));

        StockQuantity -= quantity;
        return Result<int>.Success(StockQuantity);
    }

    public void RestockProduct(int quantity)
    {
        if (quantity <= 0)
            throw new ValidationException("quantity", "Restock quantity must be greater than zero");

        StockQuantity += quantity;
    }

    public Result RestockProductSafe(int quantity)
    {
        if (quantity <= 0)
            return Result.Failure(
                new OrderValidationError("quantity", "Restock quantity must be greater than zero")
            );

        StockQuantity += quantity;
        return Result.Success();
    }

    public void UpdatePrice(Money? newPrice)
    {
        if (newPrice is null)
            throw new ArgumentNullException(nameof(newPrice));

        if (newPrice.Amount <= 0)
            throw new BusinessRuleException(
                "POSITIVE_PRICE",
                "Product price must be greater than zero"
            );

        if (newPrice.Currency != Price.Currency)
            throw new BusinessRuleException(
                "CURRENCY_CHANGE",
                $"Cannot change currency from {Price.Currency} to {newPrice.Currency}"
            );

        Price = newPrice;
    }

    public Result UpdatePriceSafe(Money? newPrice)
    {
        if (newPrice is null)
            return Result.Failure("NULL_VALUE", "New price cannot be null");

        if (newPrice.Amount <= 0)
            return Result.Failure(
                new BusinessRuleError("POSITIVE_PRICE", "Product price must be greater than zero")
            );

        if (newPrice.Currency != Price.Currency)
            return Result.Failure(
                new BusinessRuleError(
                    "CURRENCY_CHANGE",
                    $"Cannot change currency from {Price.Currency} to {newPrice.Currency}"
                )
                    .WithMetadata("currentCurrency", Price.Currency)
                    .WithMetadata("newCurrency", newPrice.Currency)
            );

        Price = newPrice;
        return Result.Success();
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidStateTransitionException("Inactive", "Inactive", nameof(Product));

        IsActive = false;
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Failure("ALREADY_ACTIVE", "Product is already active");

        if (StockQuantity <= 0)
            return Result.Failure(
                new BusinessRuleError("NO_STOCK", "Cannot activate product with no stock")
            );

        IsActive = true;
        return Result.Success();
    }
}
