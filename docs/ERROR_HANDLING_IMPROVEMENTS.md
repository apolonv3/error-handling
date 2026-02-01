# Error Handling – Areas for Further Improvement

This document lists parts of the codebase where error handling can be updated or improved.

---

## 1. **API layer – Use typed errors everywhere** ✅ Implemented

**Current:** ~~`OrdersResultController` still uses the base `Error.Validation(...)` in one place and `result.Error!` (null-forgiving) in several places.~~ **Done.**

**Implemented:**
- `GetOrder` already used `OrderValidationError("orderId", "Invalid order ID")`; no base `Error.Validation` remains in the controller.
- All `result.Error!` usages removed: **SubmitOrder**, **CancelOrder**, **ProcessPayment**, and **ProcessOrderWorkflow** now use `result.Match(onSuccess, onFailure)` so the failure branch receives the error as a parameter (no null-forgiving).
- Added `Result.Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure)` on the base `Result` class so unit results (e.g. `CancelOrderAsync`) can use Match as well.

**Files:** `src/ErrorHandling.Api/Controllers/OrdersResultController.cs`, `src/ErrorHandling.Domain/Results/Result.cs`

---

## 2. **Entity-level errors – Align with OrderError where useful** ✅ Implemented (Option B)

**Implemented (Option B):** Entity-level errors from order-scoped operations now return typed `OrderError`. The service then does `return Result<Order>.Failure(reserveResult.Error!)` and the API maps via `Error.Type` (fallback in `GetStatusAndType`).

**Improve:**
- Option A: Keep entities generic (no dependency on `OrderError`) and document that entity errors are mapped by `Error.Type` and subclasses in the API. No code change.
- Option B: Where an entity error is clearly order-scoped (e.g. `Order.SubmitSafe()` returns “order must have items”), you could introduce a small set of shared domain error types (or have entities return a union type) so the API can switch on the same types. This increases type safety but adds coupling between entities and the order error model.

**Files:** `src/ErrorHandling.Domain/Results/OrderErrors.cs`, `Entities/Order.cs`, `Product.cs`, `Customer.cs`, `ErrorHandling.Api/Extensions/ResultExtensions.cs`

---

## 3. **Result&lt;TValue, TError&gt; – Use for order operations**

**Current:** `ResultOrderService` returns `Result<Order>` and `Result` with a single `Error` type. The API must handle `Error` and pattern-match on subclasses.

**Improve:**
- Use `Result<Order, OrderError>` (and `Result<OrderError>` for unit results) for order operations. Then:
  - The compiler enforces that only `OrderError` can appear.
  - API and middleware can switch on `OrderError` / `OrderErrorCode` exhaustively.
- Add async support for `Result<TValue, TError>` in the Domain (e.g. `BindAsync`, `MapAsync`) so services can keep a railway-oriented style while returning `Result<Order, OrderError>`.

**Files:** `src/ErrorHandling.Domain/Results/Result.cs`, `ResultExtensions.cs`, `ResultOrderService.cs`, `OrdersResultController.cs`, `ResultExtensions.cs` (API)

---

## 4. **CancellationToken – Propagate through repositories and services**

**Current:** Repository and service methods do not accept `CancellationToken`, so HTTP request cancellation is not propagated to data access or domain logic.

**Improve:**
- Add `CancellationToken cancellationToken = default` to repository interface methods and implementations.
- Add it to `ResultOrderService` (and optionally `ExceptionOrderService`) and pass it through to repositories.
- Controllers already have `HttpContext.RequestAborted`; pass it as the cancellation token into the service.

**Files:** `src/ErrorHandling.Domain/Repositories/*.cs`, `src/ErrorHandling.Api/Infrastructure/InMemoryRepositories.cs`, `ResultOrderService.cs`, `OrdersResultController.cs`

---

## 5. **Value objects (Money, Email) – Typed validation errors**

**Current:** `Money.TryCreate` and `Email.TryCreate` return `Result<T>.Failure(Error.Validation(...))` with string codes and messages.

**Improve:**
- Introduce a small set of value-object validation errors (e.g. `MoneyValidationError`, `EmailValidationError`) with fields like `Field`, `AttemptedValue`, so the API can include them in Problem Details in a structured way. Alternatively, keep the current approach and rely on `Error.Type` and `Error.Metadata`; then the only improvement is documenting that convention.

**Files:** `src/ErrorHandling.Domain/ValueObjects/Money.cs`, `Email.cs`

---

## 6. **GlobalExceptionMiddleware – Exhaustiveness and consistency**

**Current:** Middleware maps domain exceptions to Problem Details. It already handles many exception types; `AggregateNotFoundException` and `DuplicateEntityException` exist and are handled.

**Improve:**
- Add a final `_ =>` branch that logs the unhandled exception type and still returns 500, so new domain exception types are visible in logs until they are explicitly mapped.
- Optionally add a small integration test that throws each domain exception and asserts status code and problem details shape, so new exceptions don’t fall through unhandled.

**Files:** `src/ErrorHandling.Api/Middleware/GlobalExceptionMiddleware.cs`

---

## 7. **API ResultExtensions – Exhaustive switch and logging**

**Current:** `GetStatusAndType` and `GetTitle` switch on `OrderErrorCode` with a `_` default that returns 422 / “Business Rule Violation”.

**Improve:**
- When a new `OrderErrorCode` is added, the default hides the fact that it’s not explicitly mapped. Options:
  - **Option A:** In the default branch, log a warning with the unhandled `OrderErrorCode` and then return 422.
  - **Option B:** Use a switch expression without default and let the compiler enforce exhaustiveness (and add a default only for “future” codes with a log).
- Add optional structured logging in `ConvertErrorToProblemDetails` (e.g. log `error.Code`, `error.Type`, and for `OrderError` the `CodeEnum`) so support and monitoring can rely on consistent fields.

**Files:** `src/ErrorHandling.Api/Extensions/ResultExtensions.cs`

---

## 8. **CompositeError and multi-field validation**

**Current:** `Result.Combine` and `CompositeError` exist; `ResultExtensions` writes `problemDetails.Extensions["errors"]` for composite errors. Controllers and services rarely collect multiple validation failures into one `Result` before returning.

**Improve:**
- In request handlers, consider validating all input (e.g. customer ID, shipping address, items) and combining results with `Result.Combine` or a custom “collect validation errors” helper, then return one 400 response with multiple field errors. This improves UX and keeps the type-safe error model.

**Files:** `OrdersResultController.cs`, `ResultOrderService.cs`, `ResultExtensions.cs`

---

## 9. **Exception-based controller – Align with domain exceptions**

**Current:** `OrdersExceptionController` uses `ExceptionOrderService`; one place throws `ArgumentException("Invalid order ID")` instead of a domain exception.

**Improve:**
- Replace `throw new ArgumentException("Invalid order ID")` with `throw new ValidationException("orderId", "Invalid order ID")` (or your standard validation exception) so the exception path is fully domain-oriented and middleware can map it consistently.

**Files:** `src/ErrorHandling.Api/Controllers/OrdersExceptionController.cs`

---

## 10. **Documentation and conventions**

**Improve:**
- Add a short “Error handling” section to the main README: when to use Result vs exceptions, how `OrderError` maps to HTTP, and where to add new error types (Domain vs API).
- Optionally add a one-page “Error catalog”: list of `OrderErrorCode` and corresponding HTTP status and when each is used.

**Files:** `README.md`, optionally `docs/ERROR_CATALOG.md`

---

## Summary table

| Area                         | Priority | Effort | Impact |
|-----------------------------|----------|--------|--------|
| API: typed errors + avoid `Error!` | High     | Low    | Type safety, fewer nulls |
| CancellationToken           | High     | Medium | Correct cancellation |
| Result&lt;T, OrderError&gt;   | Medium   | Medium | Stronger typing end-to-end |
| Entity errors vs OrderError  | Medium   | Medium | Consistency (optional) |
| Exception controller fix   | Low      | Low    | Consistency |
| Exhaustive switch + logging | Low      | Low    | Maintainability |
| Value object typed errors   | Low      | Low    | Consistency (optional) |
| Composite validation        | Low      | Medium | UX |
| Docs and catalog            | Low      | Low    | Onboarding |

Recommendation: do **§1 (API typed errors)**, **§9 (exception controller)**, and **§7 (exhaustive default + logging)** first; then **§4 (CancellationToken)**; then consider **§3 (Result&lt;T, OrderError&gt;)** and **§2** if you want maximum type safety.
