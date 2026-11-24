using System;
using System.Text.Json;

using Examples.DomainModeling;
using Examples.JsonSerialization;
using LondoSpark.Func;

// System.Text.Json does not know the union cases, so an option or result needs a
// converter before it can cross a process or network boundary. This example shows
// the shape a library-provided converter would need.
//
//   dotnet run --project examples/JsonSerialization

var options = new JsonSerializerOptions { WriteIndented = true };
options.Converters.Add(new OptionJsonConverter<int>());
options.Converters.Add(new OptionJsonConverter<string?>());
options.Converters.Add(new ResultJsonConverter<Customer, string>());

Option<int> some = Option.Return(42);
Option<int> none = Option.Empty<int>();
Option<string?> someNull = Option.Return<string?>(null);

Console.WriteLine($"some:      {JsonSerializer.Serialize(some, options)}");
Console.WriteLine($"none:      {JsonSerializer.Serialize(none, options)}");
Console.WriteLine($"some null: {JsonSerializer.Serialize(someNull, options)}");

var someJson = JsonSerializer.Serialize(some, options);
var someRoundTrip = JsonSerializer.Deserialize<Option<int>>(someJson, options);
Console.WriteLine($"round-trip: {JsonSerializer.Serialize(someRoundTrip, options)}");

var customer = new Customer(1, "Ada", "ada@example.com", 36);
Result<Customer, string> success = Result.Return<Customer, string>(customer);
Result<Customer, string> failure = Result.Failure<Customer, string>("A customer with email 'ada@example.com' already exists.");

Console.WriteLine($"success:   {JsonSerializer.Serialize(success, options)}");
Console.WriteLine($"failure:   {JsonSerializer.Serialize(failure, options)}");
var failureJson = JsonSerializer.Serialize(failure, options);
var failureRoundTrip = JsonSerializer.Deserialize<Result<Customer, string>>(failureJson, options);
Console.WriteLine($"round-trip: {JsonSerializer.Serialize(failureRoundTrip, options)}");
