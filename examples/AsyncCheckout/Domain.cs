using LondoSpark.Func;

namespace Examples.AsyncCheckout;

/// <summary>An order as it arrives, before anything has checked it.</summary>
public sealed record Order(string Customer, string Sku, int Quantity);

/// <summary>What a successful checkout hands back.</summary>
public sealed record Receipt(string Payment, decimal Charged, string Tracking);

/// <summary>The order itself is unusable.</summary>
public sealed record InvalidOrder(string Message);

/// <summary>There is not enough stock to reserve.</summary>
public sealed record OutOfStock(string Sku, int Available);

/// <summary>The payment provider refused the charge.</summary>
public sealed record CardDeclined(string Reason);

/// <summary>Every way a checkout can be refused: one failure type for the whole pipeline.</summary>
public union CheckoutError(InvalidOrder, OutOfStock, CardDeclined);

/// <summary>A result whose failure is a <see cref="CheckoutError"/>, which a synchronous <c>async</c> method can return.</summary>
[AsyncResult<CheckoutError>]
public readonly partial struct Checked<T>;
