using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using LondoSpark.Func;

using static LondoSpark.Func.Prelude;

namespace Examples.AsyncCheckout;

/// <summary>Checks out an order through three asynchronous services with one query.</summary>
public sealed class Checkout(Inventory inventory, Payments payments, Shipping shipping)
{
    // Each from clause is a plain Result, a Task<Result>, or a ValueTask<Result>. The first failure ends the query:
    // a declined card never books a courier, and an invalid order never reaches the network.
    public ValueTask<Result<Receipt, CheckoutError>> RunAsync(Order order) =>
        from valid in Validate(order)
        from total in inventory.ReserveAsync(valid.Sku, valid.Quantity)
        from payment in payments.ChargeAsync(valid.Customer, total)
        from tracking in shipping.ScheduleAsync(valid)
        select new Receipt(payment, total, tracking);

    // The same checkout as ordinary async code. Each await of a result unwraps its success or returns its failure
    // from the method; Unwrap() waits for a service and then unwraps what it answered.
    [AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))]
    public async ValueTask<Result<Receipt, CheckoutError>> RunStepByStepAsync(Order order)
    {
        var valid = await Validate(order);
        var total = await inventory.ReserveAsync(valid.Sku, valid.Quantity).Unwrap();
        var payment = await payments.ChargeAsync(valid.Customer, total).Unwrap();
        var tracking = await shipping.ScheduleAsync(valid).Unwrap();
        return success(new Receipt(payment, total, tracking));
    }

    // C# accepts only task-like types with one type parameter, so a synchronous async method returns Checked<T>, a
    // result with the failure type fixed. await failure(...) ends it with that failure.
    private static async Checked<Order> Validate(Order order)
    {
        if (order.Customer.Length == 0)
            await failure<CheckoutError>(new InvalidOrder("a customer is required"));

        if (order.Quantity <= 0)
            await failure<CheckoutError>(new InvalidOrder("quantity must be positive"));

        return order;
    }
}
