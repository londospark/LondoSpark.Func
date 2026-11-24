using System;
using System.Collections.Generic;
using System.Linq;

using Examples.DomainModeling;
using LondoSpark.Func;

// A tiny CLI that keeps absence and expected failure explicit from argument parsing
// through to the process exit code. The directory is seeded so every command has
// something to work with.
//
//   dotnet run --project examples/ConsolePipeline -- find 1
//   dotnet run --project examples/ConsolePipeline -- add Ada ada@example.com 36
//   dotnet run --project examples/ConsolePipeline -- add Ada ada@example.com 36  (duplicate)

var directory = new CustomerDirectory();
_ = Register(directory, new CustomerRequest("Ada", "ada@example.com", "36"));
_ = Register(directory, new CustomerRequest("Grace", "grace@example.com", "45"));

var outcome = args.FirstOrDefault().ToOption() switch
{
    Some<string> { Value: "find" } => Find(directory, args),
    Some<string> { Value: "add" } => Register(directory, new CustomerRequest(
        args.ElementAtOrDefault(1),
        args.ElementAtOrDefault(2),
        args.ElementAtOrDefault(3))),
    Some<string> { Value: "list" } => Result.Return<string, string>(
        string.Join(Environment.NewLine, directory.All.Select(Describe))),
    Some<string> { Value: var unknown } => Result.Failure<string, string>($"Unknown command '{unknown}'."),
    None => Result.Failure<string, string>("Usage: find <id> | add <name> <email> <age> | list")
};

// ErrorResult focuses composition on the failure case without disturbing success.
return outcome
    .AsErrorResult()
    .Select(error => $"ConsolePipeline: {error}")
    .ToResult() switch
{
    Success<string> success => Report(0, success.Value),
    Failure<string> failure => Report(1, failure.Value)
};

Result<string, string> Find(CustomerDirectory directory, string[] args) =>
    args.ElementAtOrDefault(1).ToOption() switch
    {
        Some<string> { Value: var text } when int.TryParse(text, out var id) =>
            directory.Find(id) switch
            {
                Some<Customer> { Value: var customer } => Result.Return<string, string>(Describe(customer)),
                None => Result.Failure<string, string>($"No customer with id {id}.")
            },
        Some<string> { Value: var text } => Result.Failure<string, string>($"'{text}' is not a customer id."),
        None => Result.Failure<string, string>("find requires a customer id.")
    };

static Result<string, string> Register(CustomerDirectory directory, CustomerRequest request) =>
    CustomerValidation.Validate(request) switch
    {
        Success<NewCustomer> { Value: var candidate } =>
            directory.Register(candidate) switch
            {
                Success<Customer> { Value: var registered } =>
                    Result.Return<string, string>($"Registered customer {registered.Id}."),
                Failure<string> { Value: var error } => Result.Failure<string, string>(error)
            },
        Failure<NonEmptyList<string>> errors =>
            Result.Failure<string, string>(string.Join(" ", errors.Value))
    };

static string Describe(Customer customer) =>
    $"#{customer.Id} {customer.Name} <{customer.Email}>, age {customer.Age}";

static int Report(int exitCode, string message)
{
    Console.WriteLine(message);
    return exitCode;
}
