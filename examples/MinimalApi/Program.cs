using System.Linq;

using Examples.DomainModeling;
using LondoSpark.Func;
using LondoSpark.Func.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Hosting;

// Option describes not-found without a nullable reference, Result describes an
// expected conflict, and Validation describes a request with field-keyed problems.
// The ASP.NET helpers in LondoSpark.Func.AspNetCore turn those cases into responses.
//
//   dotnet run --project examples/MinimalApi
//   curl -X POST localhost:5000/customers -H 'content-type: application/json' \
//        -d '{"name":"Ada","email":"ada@example.com","age":"36"}'
//   curl localhost:5000/customers/1

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var directory = new CustomerDirectory();

app.MapGet("/customers", Ok<Customer[]> () => TypedResults.Ok(directory.All.ToArray()));

app.MapGet("/customers/{id:int}", IResult (int id) =>
    directory.Find(id).ToHttpResult());

// ToHttpResult is the shorthand when a value maps in one call. When the flow has
// steps, a switch expression reads better and proves itself exhaustive: adding a
// case to either union stops this compiling until the new case is handled.
app.MapPost("/customers", IResult (CustomerRequest request) =>
    CustomerValidation.Validate(request) switch
    {
        Success<NewCustomer> validated => directory.Register(validated.Value) switch
        {
            Success<Customer> customer =>
                TypedResults.Created($"/customers/{customer.Value.Id}", customer.Value),
            Failure<ValidationError> failure => TypedResults.Conflict(failure.Value.Message)
        },
        Failure<NonEmptyList<ValidationError>> errors => errors.Value.ToValidationProblem()
    });

app.Run();
