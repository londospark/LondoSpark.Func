using System;
using System.Linq;

using Examples.DomainModeling;
using LondoSpark.Func;
using static LondoSpark.Func.Option<string>;
using static LondoSpark.Func.Prelude;
using static LondoSpark.Func.Result<string, string>;
using FoundCustomer = LondoSpark.Func.Option<Examples.DomainModeling.Customer>;

// A tiny CLI that keeps absence and expected failure explicit from argument parsing
// through to the process exit code. The directory is seeded so every command has
// something to work with.
//
// The static imports bring each union's cases into scope, so the switches below name
// Some, Success, and Failure without type arguments and stay exhaustive. Option<string>
// and Option<Customer> both have a Some case, so the second is reached through an alias.
// The prelude's lowercase success(...) and failure(...) build results without new or
// type arguments; the surrounding return type supplies the other side.
//
//   dotnet run --project examples/ConsolePipeline -- find 1
//   dotnet run --project examples/ConsolePipeline -- add Ada ada@example.com 36
//   dotnet run --project examples/ConsolePipeline -- add Ada ada@example.com 36  (duplicate)

var directory = new CustomerDirectory();
_ = Register(directory, new CustomerRequest("Ada", "ada@example.com", "36"));
_ = Register(directory, new CustomerRequest("Grace", "grace@example.com", "45"));

var outcome = args.FirstOrDefault().ToOption() switch
{
    Some("find") => Find(directory, args),
    Some("add") => Register(directory, new CustomerRequest(
        args.ElementAtOrDefault(1),
        args.ElementAtOrDefault(2),
        args.ElementAtOrDefault(3))),
    Some("list") => success(string.Join(Environment.NewLine, directory.All.Select(Describe))),
    Some(var unknown) => failure($"Unknown command '{unknown}'."),
    None => failure("Usage: find <id> | add <name> <email> <age> | list")
};

// ErrorResult focuses composition on the failure case without disturbing success.
var report = outcome
    .AsErrorResult()
    .Select(error => $"ConsolePipeline: {error}")
    .ToResult();

Console.WriteLine(report switch
{
    Success(var message) => message,
    Failure(var message) => message
});

return report.ToExitCode();

static Result<string, string> Find(CustomerDirectory directory, string[] args) =>
    args.ElementAtOrDefault(1).ToOption() switch
    {
        Some(var text) when int.TryParse(text, out var id) => directory.Find(id) switch
        {
            FoundCustomer.Some(var customer) => success(Describe(customer)),
            None => failure($"No customer with id {id}.")
        },
        Some(var text) => failure($"'{text}' is not a customer id."),
        None => failure("find requires a customer id.")
    };

// Validate and register as one pipeline, then translate the domain error for the console.
static Result<string, string> Register(CustomerDirectory directory, CustomerRequest request) =>
    CustomerValidation.Validate(request)
        .Bind(directory.Register)
        .Select(static customer => $"Registered customer {customer.Id}.")
        .MapError(static error => error switch
        {
            InvalidRequest invalid => invalid.Errors.ToSeparatedString(" "),
            AlreadyRegistered taken => $"Email '{taken.Email}' is already registered."
        });

static string Describe(Customer customer) =>
    $"#{customer.Id} {customer.Name} <{customer.Email}>, age {customer.Age}";
