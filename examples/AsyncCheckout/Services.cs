using System.Collections.Generic;
using System.Threading.Tasks;

using LondoSpark.Func;

using static LondoSpark.Func.Prelude;

namespace Examples.AsyncCheckout;

/// <summary>Records which services a checkout reached, so the console can show where a failure stopped it.</summary>
public sealed class CallLog
{
    public List<string> Calls { get; } = [];

    public void Add(string call) => Calls.Add(call);
}

/// <summary>A stock service behind a network call: it answers with a <see cref="Task{TResult}"/>.</summary>
public sealed class Inventory(CallLog log)
{
    private readonly Dictionary<string, (int Stock, decimal Price)> _items = new()
    {
        ["kettle"] = (5, 24.99m),
        ["toaster"] = (0, 39.00m),
    };

    public async Task<Result<decimal, CheckoutError>> ReserveAsync(string sku, int quantity)
    {
        await Task.Delay(20);
        log.Add($"inventory: reserve {quantity} x {sku}");
        var (stock, price) = _items[sku];
        return stock >= quantity
            ? success(price * quantity)
            : failure<CheckoutError>(new OutOfStock(sku, stock));
    }
}

/// <summary>A payment provider whose fraud check answers synchronously: it returns a <see cref="ValueTask{TResult}"/>.</summary>
public sealed class Payments(CallLog log)
{
    public ValueTask<Result<string, CheckoutError>> ChargeAsync(string customer, decimal amount)
    {
        log.Add($"payments: charge {customer} {amount:0.00}");

        // Declines are decided locally and complete without suspending; approvals go to the network.
        return amount > 100m
            ? ValueTask.FromResult<Result<string, CheckoutError>>(failure<CheckoutError>(new CardDeclined("over the card limit")))
            : new ValueTask<Result<string, CheckoutError>>(ApproveAsync(customer));
    }

    private static async Task<Result<string, CheckoutError>> ApproveAsync(string customer)
    {
        await Task.Delay(20);
        return success($"pay_{customer.ToUpperInvariant()}_001");
    }
}

/// <summary>A courier booking, also behind a network call.</summary>
public sealed class Shipping(CallLog log)
{
    public async Task<Result<string, CheckoutError>> ScheduleAsync(Order order)
    {
        await Task.Delay(20);
        log.Add($"shipping: book {order.Quantity} x {order.Sku}");
        return success($"TRK-{order.Sku.Length}{order.Quantity}42");
    }
}
