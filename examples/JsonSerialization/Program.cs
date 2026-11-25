using System;
using System.Text.Json;

using Examples.DomainModeling;
using LondoSpark.Func;
using LondoSpark.Func.Json;

using static LondoSpark.Func.Prelude;

// System.Text.Json does not know the union cases, so the LondoSpark.Func.Json package
// supplies converters. Registering the two factories covers every closed generic type.
//
//   dotnet run --project examples/JsonSerialization

var options = new JsonSerializerOptions { WriteIndented = true };
options.AddLondoSparkFuncConverters();

var present = some(42);
Option<int> absent = none;
var presentNull = some<string?>(null);

Console.WriteLine($"some:      {JsonSerializer.Serialize(present, options)}");
Console.WriteLine($"none:      {JsonSerializer.Serialize(absent, options)}");
Console.WriteLine($"some null: {JsonSerializer.Serialize(presentNull, options)}");

var someJson = JsonSerializer.Serialize(present, options);
var someRoundTrip = JsonSerializer.Deserialize<Option<int>>(someJson, options);
Console.WriteLine($"round-trip: {someRoundTrip}");

var customer = new Customer(1, "Ada", "ada@example.com", 36);
Result<Customer, string> registered = success(customer);
Result<Customer, string> refused = failure("A customer with email 'ada@example.com' already exists.");

Console.WriteLine($"success:   {JsonSerializer.Serialize(registered, options)}");
Console.WriteLine($"failure:   {JsonSerializer.Serialize(refused, options)}");

var failureJson = JsonSerializer.Serialize(refused, options);
var failureRoundTrip = JsonSerializer.Deserialize<Result<Customer, string>>(failureJson, options);
Console.WriteLine($"round-trip: {failureRoundTrip}");
