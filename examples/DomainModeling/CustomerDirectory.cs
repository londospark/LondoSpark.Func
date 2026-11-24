using System;
using System.Collections.Generic;

using LondoSpark.Func;

namespace Examples.DomainModeling;

/// <summary>In-memory store showing <see cref="Option{TSome}"/> for lookup and <see cref="Result{TSuccess, TFailure}"/> for expected failure.</summary>
public sealed class CustomerDirectory
{
    private readonly Dictionary<int, Customer> _customers = [];
    private readonly Dictionary<string, int> _idsByEmail = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId = 1;

    /// <summary>Gets every registered customer.</summary>
    public IReadOnlyCollection<Customer> All => _customers.Values;

    /// <summary>Finds a customer by identity, or returns <see cref="None"/> when there is no such customer.</summary>
    /// <param name="id">The customer identity.</param>
    /// <returns>The matching customer, or absence.</returns>
    public Option<Customer> Find(int id) =>
        _customers.TryGetValue(id, out var customer)
            ? Option.Return(customer)
            : Option.Empty<Customer>();

    /// <summary>Registers a validated customer, or reports why registration is not possible.</summary>
    /// <param name="candidate">The validated customer.</param>
    /// <returns>The registered customer, or a failure describing the conflict.</returns>
    public Result<Customer, string> Register(NewCustomer candidate) =>
        _idsByEmail.ContainsKey(candidate.Email)
            ? Result.Failure<Customer, string>($"A customer with email '{candidate.Email}' already exists.")
            : Add(candidate);

    private Result<Customer, string> Add(NewCustomer candidate)
    {
        var customer = new Customer(_nextId++, candidate.Name, candidate.Email, candidate.Age);
        _customers.Add(customer.Id, customer);
        _idsByEmail.Add(customer.Email, customer.Id);
        return Result.Return<Customer, string>(customer);
    }
}
