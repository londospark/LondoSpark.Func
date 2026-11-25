using System;

using LondoSpark.Func;

using static LondoSpark.Func.Prelude;

namespace Examples.DomainModeling;

/// <summary>Collects every problem with a registration request instead of stopping at the first one.</summary>
public static class CustomerValidation
{
    // The curried constructor seeds the applicative chain. Typed lambda parameters give it a natural type,
    // so success infers it, and the field's declared type completes the validation.
    private static readonly Validation<Func<string, Func<string, Func<int, NewCustomer>>>, ValidationError> NewCustomerFrom =
        success((string name) => (string email) => (int age) => new NewCustomer(name, email, age));

    /// <summary>Validates a request applicatively, then hands the accumulated errors to the domain pipeline.</summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>The validated customer, or one domain error holding every field problem found.</returns>
    public static Result<NewCustomer, CustomerError> Validate(CustomerRequest request) =>
        NewCustomerFrom
            .Apply(RequiredName(request.Name))
            .Apply(RequiredEmail(request.Email))
            .Apply(RequiredAge(request.Age))
            .ToResult()
            .MapError(static errors => (CustomerError)new InvalidRequest(errors));

    private static Validation<string, ValidationError> RequiredName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? failure(new ValidationError("name", "Name is required."))
            : success(name);

    private static Validation<string, ValidationError> RequiredEmail(string? email) =>
        email is null || !email.Contains('@', StringComparison.Ordinal)
            ? failure(new ValidationError("email", "A valid email address is required."))
            : success(email);

    private static Validation<int, ValidationError> RequiredAge(string? age) =>
        int.TryParse(age, out var parsed) && parsed is >= 0 and <= 130
            ? success(parsed)
            : failure(new ValidationError("age", "Age must be a whole number between 0 and 130."));
}
