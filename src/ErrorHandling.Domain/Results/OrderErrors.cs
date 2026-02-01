using ErrorHandling.Domain.Entities;
using ErrorHandling.Domain.ValueObjects;

namespace ErrorHandling.Domain.Results;

/// <summary>
/// Type-safe error codes for order operations.
/// </summary>
/// <remarks>
/// Replaces string-based error codes so that API and services can use exhaustive
/// pattern matching (switch on CodeEnum) and the compiler can enforce handling of all cases.
/// </remarks>
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
/// Base type for all order-related domain errors.
/// </summary>
/// <remarks>
/// Extends <see cref="Error"/> with a strongly-typed <see cref="OrderErrorCode"/> so that
/// API layer can pattern-match on error kind and map to HTTP status and problem details
/// without relying on string codes.
/// </remarks>
public abstract class OrderError : Error
{
    public OrderErrorCode CodeEnum { get; }

    protected OrderError(OrderErrorCode codeEnum, string code, string message, ErrorType type = ErrorType.Failure)
        : base(code, message, type)
    {
        CodeEnum = codeEnum;
    }
}

/// <summary>
/// Validation failed for a specific input field.
/// </summary>
/// <remarks>
/// Used when request data fails validation (e.g. empty customer ID, negative quantity).
/// Maps to HTTP 400 Bad Request.
/// </remarks>
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

/// <summary>
/// An entity was not found by its identifier.
/// </summary>
/// <remarks>
/// Used when Customer, Order, or Product lookup by ID returns null.
/// Maps to HTTP 404 Not Found.
/// </remarks>
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

/// <summary>
/// Customer is not in active status and cannot place or modify orders.
/// </summary>
/// <remarks>
/// Used when creating an order for a suspended or closed customer.
/// Maps to HTTP 422 Unprocessable Entity.
/// </remarks>
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

/// <summary>
/// Product is not available for purchase (e.g. deactivated or out of catalog).
/// </summary>
/// <remarks>
/// Used when adding an inactive product to an order. Maps to HTTP 422 Unprocessable Entity.
/// </remarks>
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

/// <summary>
/// Insufficient stock for the requested quantity.
/// </summary>
/// <remarks>
/// Used when reserving stock for an order line exceeds available quantity.
/// Maps to HTTP 422 Unprocessable Entity.
/// </remarks>
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

/// <summary>
/// Invalid state transition for an entity (e.g. submitting an already submitted order).
/// </summary>
/// <remarks>
/// Used when an operation is not allowed in the current entity state.
/// Maps to HTTP 422 Unprocessable Entity.
/// </remarks>
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

/// <summary>
/// Payment amount is less than the order total.
/// </summary>
/// <remarks>
/// Used when processing payment and the amount does not cover the order total.
/// Maps to HTTP 422 Unprocessable Entity.
/// </remarks>
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

/// <summary>
/// A required value was null (e.g. payment amount not provided).
/// </summary>
/// <remarks>
/// Used for null checks on required inputs. Maps to HTTP 400 Bad Request.
/// </remarks>
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

/// <summary>
/// Order must have at least one line item before it can be submitted.
/// </summary>
/// <remarks>
/// Used when submitting an empty order. Maps to HTTP 422 Unprocessable Entity.
/// </remarks>
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
