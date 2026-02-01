using ErrorHandling.Domain.Exceptions;
using ErrorHandling.Domain.Results;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Domain.Entities;

public class Order
{
    private List<OrderItem> _items = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public Money TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public string ShippingAddress { get; private set; }

    private Order()
    {
        // Initialize with safe defaults for EF Core
        TotalAmount = Money.Zero();
        ShippingAddress = string.Empty;
        _items = new List<OrderItem>();
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    // Exception-based constructor
    public Order(Guid customerId, string shippingAddress)
    {
        if (customerId == Guid.Empty)
            throw new ValidationException("customerId", "Customer ID is required");

        if (string.IsNullOrWhiteSpace(shippingAddress))
            throw new ValidationException("shippingAddress", "Shipping address is required");

        Id = Guid.NewGuid();
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        TotalAmount = Money.Zero();
    }

    // Result-based factory
    public static Result<Order> Create(Guid customerId, string shippingAddress)
    {
        if (customerId == Guid.Empty)
            return Result<Order>.Failure(new OrderValidationError("customerId", "Customer ID is required"));

        if (string.IsNullOrWhiteSpace(shippingAddress))
            return Result<Order>.Failure(
                new OrderValidationError("shippingAddress", "Shipping address is required")
            );

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ShippingAddress = shippingAddress,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            TotalAmount = Money.Zero(),
        };

        return Result<Order>.Success(order);
    }

    // Exception-based method
    public void AddItem(Product product, int quantity)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));

        if (quantity <= 0)
            throw new ValidationException("quantity", "Quantity must be greater than zero");

        if (Status != OrderStatus.Pending)
            throw new InvalidStateTransitionException(
                Status.ToString(),
                "AddingItem",
                nameof(Order)
            );

        var existingItem = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existingItem != null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            _items.Add(new OrderItem(product.Id, product.Name, product.Price, quantity));
        }

        RecalculateTotal();
    }

    // Result-based method
    public Result AddItemSafe(Product product, int quantity)
    {
        if (product == null)
            return Result.Failure(new NullValueError("product", "Product cannot be null"));

        if (quantity <= 0)
            return Result.Failure(
                new OrderValidationError("quantity", "Quantity must be greater than zero")
            );

        if (Status != OrderStatus.Pending)
            return Result.Failure(
                new OrderInvalidStateTransitionError(Status.ToString(), "AddingItem", nameof(Order))
            );

        var existingItem = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existingItem != null)
        {
            var increaseResult = existingItem.TryIncreaseQuantity(quantity);
            if (increaseResult.IsFailure)
                return increaseResult;
        }
        else
        {
            var itemResult = OrderItem.Create(product.Id, product.Name, product.Price, quantity);
            if (itemResult.IsFailure)
                return Result.Failure(itemResult.Error!);

            _items.Add(itemResult.Value);
        }

        return RecalculateTotalSafe();
    }

    public void RemoveItem(Guid productId)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidStateTransitionException(
                Status.ToString(),
                "RemovingItem",
                nameof(Order)
            );

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            throw new EntityNotFoundException("OrderItem", productId);

        _items.Remove(item);
        RecalculateTotal();
    }

    public void Submit()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidStateTransitionException(
                Status.ToString(),
                OrderStatus.Submitted.ToString(),
                nameof(Order)
            );

        if (!_items.Any())
            throw new InvariantViolationException(
                "ORDER_MUST_HAVE_ITEMS",
                "Cannot submit an order without items",
                _items.Count,
                ">0"
            );

        Status = OrderStatus.Submitted;
    }

    public Result SubmitSafe()
    {
        if (Status != OrderStatus.Pending)
            return Result.Failure(
                new OrderInvalidStateTransitionError(Status.ToString(), OrderStatus.Submitted.ToString(), nameof(Order))
            );

        if (!_items.Any())
            return Result.Failure(new OrderMustHaveItemsError());

        Status = OrderStatus.Submitted;
        return Result.Success();
    }

    public void Approve()
    {
        if (Status != OrderStatus.Submitted)
            throw new InvalidStateTransitionException(
                Status.ToString(),
                OrderStatus.Approved.ToString(),
                nameof(Order)
            );

        Status = OrderStatus.Approved;
    }

    public Result ApproveSafe()
    {
        if (Status != OrderStatus.Submitted)
            return Result.Failure(
                new OrderInvalidStateTransitionError(Status.ToString(), OrderStatus.Approved.ToString(), nameof(Order))
            );

        Status = OrderStatus.Approved;
        return Result.Success();
    }

    public void Ship()
    {
        if (Status != OrderStatus.Approved)
            throw new InvalidStateTransitionException(
                Status.ToString(),
                OrderStatus.Shipped.ToString(),
                nameof(Order)
            );

        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
    }

    public Result ShipSafe()
    {
        if (Status != OrderStatus.Approved)
            return Result.Failure(
                new OrderInvalidStateTransitionError(Status.ToString(), OrderStatus.Approved.ToString(), nameof(Order))
            );

        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void Cancel(string reason)
    {
        if (
            Status == OrderStatus.Shipped
            || Status == OrderStatus.Delivered
            || Status == OrderStatus.Cancelled
        )
            throw new InvalidStateTransitionException(
                Status.ToString(),
                OrderStatus.Cancelled.ToString(),
                nameof(Order)
            );

        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("reason", "Cancellation reason is required");

        Status = OrderStatus.Cancelled;
    }

    public Result CancelSafe(string reason)
    {
        if (
            Status == OrderStatus.Shipped
            || Status == OrderStatus.Delivered
            || Status == OrderStatus.Cancelled
        )
            return Result.Failure(
                new OrderInvalidStateTransitionError(Status.ToString(), OrderStatus.Cancelled.ToString(), nameof(Order))
            );

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(new OrderValidationError("reason", "Cancellation reason is required"));

        Status = OrderStatus.Cancelled;
        return Result.Success();
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items.Aggregate(Money.Zero(), (total, item) => total.Add(item.TotalPrice));
    }

    private Result RecalculateTotalSafe()
    {
        var total = Money.Zero();
        foreach (var item in _items)
        {
            var addResult = total.TryAdd(item.TotalPrice);
            if (addResult.IsFailure)
                return Result.Failure(addResult.Error!);
            total = addResult.Value;
        }

        TotalAmount = total;
        return Result.Success();
    }
}

public class OrderItem
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public Money UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public Money TotalPrice { get; private set; }

    internal OrderItem(Guid productId, string productName, Money unitPrice, int quantity)
    {
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
        TotalPrice = Money.Create(unitPrice.Amount * quantity, unitPrice.Currency);
    }

    public static Result<OrderItem> Create(
        Guid productId,
        string productName,
        Money unitPrice,
        int quantity
    )
    {
        if (productId == Guid.Empty)
            return Result<OrderItem>.Failure(
                new OrderValidationError("productId", "Product ID is required")
            );

        if (string.IsNullOrWhiteSpace(productName))
            return Result<OrderItem>.Failure(
                new OrderValidationError("productName", "Product name is required")
            );

        if (unitPrice == null!)
            return Result<OrderItem>.Failure(new NullValueError("unitPrice", "Unit price cannot be null"));

        if (quantity <= 0)
            return Result<OrderItem>.Failure(
                new OrderValidationError("quantity", "Quantity must be greater than zero")
            );

        var totalPriceResult = Money.TryCreate(unitPrice.Amount * quantity, unitPrice.Currency);
        if (totalPriceResult.IsFailure)
            return Result<OrderItem>.Failure(totalPriceResult.Error!);

        var item = new OrderItem(productId, productName, unitPrice, quantity);
        return Result<OrderItem>.Success(item);
    }

    internal void IncreaseQuantity(int additional)
    {
        if (additional <= 0)
            throw new ValidationException(
                "additional",
                "Additional quantity must be greater than zero"
            );

        Quantity += additional;
        TotalPrice = Money.Create(UnitPrice.Amount * Quantity, UnitPrice.Currency);
    }

    internal Result TryIncreaseQuantity(int additional)
    {
        if (additional <= 0)
            return Result.Failure(
                new OrderValidationError("additional", "Additional quantity must be greater than zero")
            );

        Quantity += additional;

        var totalPriceResult = Money.TryCreate(UnitPrice.Amount * Quantity, UnitPrice.Currency);
        if (totalPriceResult.IsFailure)
            return Result.Failure(totalPriceResult.Error!);

        TotalPrice = totalPriceResult.Value;
        return Result.Success();
    }
}

public enum OrderStatus
{
    Pending,
    Submitted,
    Approved,
    Shipped,
    Delivered,
    Cancelled,
}
