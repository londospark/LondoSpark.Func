using System;

using LondoSpark.Func;

namespace Examples.DomainModeling;

/// <summary>Collects every problem with a registration request instead of stopping at the first one.</summary>
public static class CustomerValidation
{
    /// <summary>Validates a request applicatively, accumulating each field failure in order.</summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>The validated customer, or every field error found.</returns>
    public static Validation<NewCustomer, ValidationError> Validate(CustomerRequest request) =>
        Validation.Return<Func<string, Func<string, Func<int, NewCustomer>>>, ValidationError>(
            name => email => age => new NewCustomer(name, email, age))
            .Apply(RequiredName(request.Name))
            .Apply(RequiredEmail(request.Email))
            .Apply(RequiredAge(request.Age));

    private static Validation<string, ValidationError> RequiredName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? Invalid<string>("name", "Name is required.")
            : Validation.Return<string, ValidationError>(name);

    private static Validation<string, ValidationError> RequiredEmail(string? email) =>
        email is null || !email.Contains('@', StringComparison.Ordinal)
            ? Invalid<string>("email", "A valid email address is required.")
            : Validation.Return<string, ValidationError>(email);

    private static Validation<int, ValidationError> RequiredAge(string? age) =>
        int.TryParse(age, out var parsed) && parsed is >= 0 and <= 130
            ? Validation.Return<int, ValidationError>(parsed)
            : Invalid<int>("age", "Age must be a whole number between 0 and 130.");

    private static Validation<T, ValidationError> Invalid<T>(string field, string message) =>
        Validation.Failure<T, ValidationError>(new ValidationError(field, message));
}
