using ErrorHandling.Domain.Entities;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Domain.Results;

/// <summary>
/// Type-safe error codes for order operations. Replaces string codes for exhaustiveness.
/// </summary>
public enum OrderErrorCode
{
    Validation,
    EntityNotFound,
    CustomerNotActive,
    ProductInactive,
    InsufficientStock,
    InvalidStateTransition,
    InsufficientPayment,
    NullValue,
    OrderMustHaveItems,
}

/// <summary>
/// Base type for all order-related errors. Enables exhaustive pattern matching.
/// </summary>
public abstract class OrderError : Error
{
    public OrderErrorCode CodeEnum { get; }

    protected OrderError(OrderErrorCode codeEnum, string code, string message, ErrorType type = ErrorType.Failure)
        : base(code, message, type)
    {
        CodeEnum = codeEnum;
    }
}

/// <summary>Validation failed for a specific field.</summary>
public sealed class OrderValidationError : OrderError
{
    public string Field { get; }

    public OrderValidationError(string field, string message)
        : base(OrderErrorCode.Validation, "VALIDATION_ERROR", message, ErrorType.Validation)
    {
        Field = field;
        WithMetadata("field", field);
    }
}

/// <summary>An entity was not found by id.</summary>
public sealed class EntityNotFoundError : OrderError
{
    public string EntityName { get; }
    public Guid Id { get; }

    public EntityNotFoundError(string entityName, Guid id)
        : base(
            OrderErrorCode.EntityNotFound,
            "NOT_FOUND",
            $"{entityName} with id '{id}' was not found",
            ErrorType.NotFound
        )
    {
        EntityName = entityName;
        Id = id;
        WithMetadata("entityName", entityName);
        WithMetadata("id", id);
    }
}

/// <summary>Customer is not in active status.</summary>
public sealed class CustomerNotActiveError : OrderError
{
    public Guid CustomerId { get; }
    public CustomerStatus Status { get; }

    public CustomerNotActiveError(Guid customerId, CustomerStatus status)
        : base(
            OrderErrorCode.CustomerNotActive,
            "INACTIVE_CUSTOMER",
            $"Customer {customerId} is not active. Current status: {status}",
            ErrorType.Failure
        )
    {
        CustomerId = customerId;
        Status = status;
        WithMetadata("customerId", customerId);
        WithMetadata("customerStatus", status);
    }
}

/// <summary>Product is not available for purchase.</summary>
public sealed class ProductInactiveError : OrderError
{
    public Guid ProductId { get; }
    public string ProductName { get; }

    public ProductInactiveError(Guid productId, string productName)
        : base(
            OrderErrorCode.ProductInactive,
            "PRODUCT_INACTIVE",
            $"Product {productName} is not available for purchase",
            ErrorType.Failure
        )
    {
        ProductId = productId;
        ProductName = productName;
        WithMetadata("productId", productId);
        WithMetadata("productName", productName);
    }
}

/// <summary>Insufficient stock for the requested quantity.</summary>
public sealed class InsufficientStockError : OrderError
{
    public int Available { get; }
    public int Requested { get; }

    public InsufficientStockError(int available, int requested)
        : base(
            OrderErrorCode.InsufficientStock,
            "INSUFFICIENT_STOCK",
            $"Insufficient stock. Available: {available}, Requested: {requested}",
            ErrorType.Failure
        )
    {
        Available = available;
        Requested = requested;
        WithMetadata("availableStock", available);
        WithMetadata("requestedQuantity", requested);
    }
}

/// <summary>Invalid state transition for an entity.</summary>
public sealed class OrderInvalidStateTransitionError : OrderError
{
    public string FromState { get; }
    public string ToState { get; }
    public string EntityType { get; }

    public OrderInvalidStateTransitionError(string fromState, string toState, string entityType)
        : base(
            OrderErrorCode.InvalidStateTransition,
            "INVALID_STATE_TRANSITION",
            $"Cannot transition from '{fromState}' to '{toState}' for {entityType}",
            ErrorType.Failure
        )
    {
        FromState = fromState;
        ToState = toState;
        EntityType = entityType;
        WithMetadata("fromState", fromState);
        WithMetadata("toState", toState);
        WithMetadata("entityType", entityType);
    }
}

/// <summary>Payment amount is less than order total.</summary>
public sealed class InsufficientPaymentError : OrderError
{
    public Money PaymentAmount { get; }
    public Money OrderTotal { get; }

    public InsufficientPaymentError(Money paymentAmount, Money orderTotal)
        : base(
            OrderErrorCode.InsufficientPayment,
            "INSUFFICIENT_PAYMENT",
            $"Payment amount {paymentAmount} is less than order total {orderTotal}",
            ErrorType.Failure
        )
    {
        PaymentAmount = paymentAmount;
        OrderTotal = orderTotal;
        WithMetadata("paymentAmount", paymentAmount);
        WithMetadata("orderTotal", orderTotal);
    }
}

/// <summary>Required value was null.</summary>
public sealed class NullValueError : OrderError
{
    public string FieldName { get; }

    public NullValueError(string fieldName, string message)
        : base(OrderErrorCode.NullValue, "NULL_VALUE", message ?? $"{fieldName} cannot be null", ErrorType.Validation)
    {
        FieldName = fieldName;
        WithMetadata("field", fieldName);
    }
}

/// <summary>Order must have at least one item to submit.</summary>
public sealed class OrderMustHaveItemsError : OrderError
{
    public OrderMustHaveItemsError()
        : base(
            OrderErrorCode.OrderMustHaveItems,
            "ORDER_MUST_HAVE_ITEMS",
            "Cannot submit an order without items",
            ErrorType.Failure
        ) { }
}
