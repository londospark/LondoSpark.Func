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
    directory.Find(id).ToOkOrNotFound());

app.MapPost("/customers", IResult (CustomerRequest request) =>
    CustomerValidation.Validate(request).ToHttpResult(
        validated => directory.Register(validated).ToHttpResult(
            static customer => TypedResults.Created($"/customers/{customer.Id}", customer),
            static error => TypedResults.Conflict(error.Message))));

app.Run();
