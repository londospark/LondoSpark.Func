using System;
using System.Linq;

using Examples.AsyncCheckout;

using static LondoSpark.Func.Result<Examples.AsyncCheckout.Receipt, Examples.AsyncCheckout.CheckoutError>;

Order[] orders =
[
    new("ada", "kettle", 2),     // succeeds
    new("grace", "toaster", 1),  // out of stock: stops at the inventory
    new("alan", "kettle", 5),    // declined: stops at the payment, no courier booked
    new("", "kettle", 1),        // invalid: stops before any service is called
];

foreach (var order in orders)
{
    var log = new CallLog();
    var checkout = new Checkout(new Inventory(log), new Payments(log), new Shipping(log));

    var result = await checkout.RunAsync(order);

    // The await version calls the same services in the same order and reaches the same outcome.
    var replay = new CallLog();
    var stepByStep = await new Checkout(new Inventory(replay), new Payments(replay), new Shipping(replay)).RunStepByStepAsync(order);
    if (!result.Equals(stepByStep) || !log.Calls.SequenceEqual(replay.Calls))
        throw new InvalidOperationException($"The query and the await version disagree for {order}.");

    // One exhaustive switch over the result, and one over the domain's failure union.
    var outcome = result switch
    {
        Success(var receipt) => $"charged {receipt.Charged:0.00}, payment {receipt.Payment}, tracking {receipt.Tracking}",
        Failure(var error) => error switch
        {
            InvalidOrder(var message) => $"rejected: {message}",
            OutOfStock(var sku, var available) => $"only {available} x {sku} left",
            CardDeclined(var reason) => $"card declined: {reason}"
        }
    };

    Console.WriteLine($"{order.Customer,-6} {order.Quantity} x {order.Sku}: {outcome}");
    foreach (var call in log.Calls)
    {
        Console.WriteLine($"         {call}");
    }
}
