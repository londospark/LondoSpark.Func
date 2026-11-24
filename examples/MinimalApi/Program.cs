using System;
using System.Collections.Generic;

using System.Linq;

using Examples.DomainModeling;
using LondoSpark.Func;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

// Option describes not-found without a nullable reference, Result describes an
// expected conflict, and Validation describes a request with several problems.
//
//   dotnet run --project examples/MinimalApi
//   curl -X POST localhost:5000/customers -H 'content-type: application/json' \
//        -d '{"name":"Ada","email":"ada@example.com","age":"36"}'
//   curl localhost:5000/customers/1

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var directory = new CustomerDirectory();

app.MapGet("/customers", Ok<Customer[]> () => TypedResults.Ok(directory.All.ToArray()));

app.MapGet("/customers/{id:int}", Results<Ok<Customer>, NotFound> (int id) =>
    directory.Find(id) switch
    {
        Some<Customer> { Value: var customer } => TypedResults.Ok(customer),
        None => TypedResults.NotFound()
    });

app.MapPost("/customers", Results<Created<Customer>, Conflict<string>, ValidationProblem> (CustomerRequest request) =>
    CustomerValidation.Validate(request) switch
    {
        Success<NewCustomer> { Value: var candidate } =>
            directory.Register(candidate) switch
            {
                Success<Customer> { Value: var registered } =>
                    TypedResults.Created($"/customers/{registered.Id}", registered),
                Failure<string> { Value: var error } => TypedResults.Conflict(error)
            },
        Failure<NonEmptyList<string>> errors =>
            TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["registration"] = [.. errors.Value]
            })
    });

app.Run();
