using System.Linq;

using Examples.DomainModeling;
using LondoSpark.Func;
using LondoSpark.Func.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Hosting;

// Option describes not-found without a nullable reference, Validation gathers every
// field problem in a request, and ToResult hands those errors to a Result pipeline.
// The ASP.NET helpers in LondoSpark.Func.AspNetCore turn the cases into responses.
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

// Validate, then register, as one pipeline: Bind stops at the first failure and the
// single switch maps the domain error, naming no generic case types.
app.MapPost("/customers", IResult (CustomerRequest request) =>
    CustomerValidation.Validate(request)
        .Bind(directory.Register)
        .ToHttpResult(
            static customer => TypedResults.Created($"/customers/{customer.Id}", customer),
            static error => error switch
            {
                InvalidRequest invalid => invalid.Errors.ToValidationProblem(),
                AlreadyRegistered taken => TypedResults.Problem(
                    $"A customer with email '{taken.Email}' already exists.",
                    statusCode: StatusCodes.Status409Conflict)
            }));

app.Run();
