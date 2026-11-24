using System;
using System.Text.Json;

using Examples.DomainModeling;
using LondoSpark.Func;
using LondoSpark.Func.Json;

// System.Text.Json does not know the union cases, so the LondoSpark.Func.Json package
// supplies converters. Registering the two factories covers every closed generic type.
//
//   dotnet run --project examples/JsonSerialization

var options = new JsonSerializerOptions { WriteIndented = true };
options.AddLondoSparkFuncConverters();

var some = Option.Return(42);
var none = Option.Empty<int>();
var someNull = Option.Return<string?>(null);

Console.WriteLine($"some:      {JsonSerializer.Serialize(some, options)}");
Console.WriteLine($"none:      {JsonSerializer.Serialize(none, options)}");
Console.WriteLine($"some null: {JsonSerializer.Serialize(someNull, options)}");

var someJson = JsonSerializer.Serialize(some, options);
var someRoundTrip = JsonSerializer.Deserialize<Option<int>>(someJson, options);
Console.WriteLine($"round-trip: {someRoundTrip}");

var customer = new Customer(1, "Ada", "ada@example.com", 36);
var success = Result.Return<Customer, string>(customer);
var failure = Result.Failure<Customer, string>("A customer with email 'ada@example.com' already exists.");

Console.WriteLine($"success:   {JsonSerializer.Serialize(success, options)}");
Console.WriteLine($"failure:   {JsonSerializer.Serialize(failure, options)}");

var failureJson = JsonSerializer.Serialize(failure, options);
var failureRoundTrip = JsonSerializer.Deserialize<Result<Customer, string>>(failureJson, options);
Console.WriteLine($"round-trip: {failureRoundTrip}");
