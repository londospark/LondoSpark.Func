using System;

using LondoSpark.Func;

namespace Examples.DomainModeling;

/// <summary>Collects every problem with a registration request instead of stopping at the first one.</summary>
public static class CustomerValidation
{
    /// <summary>Validates a request applicatively, accumulating each failure in order.</summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>The validated customer, or every error found.</returns>
    public static Validation<NewCustomer, string> Validate(CustomerRequest request) =>
        Validation.Return<Func<string, Func<string, Func<int, NewCustomer>>>, string>(
            name => email => age => new NewCustomer(name, email, age))
            .Apply(RequiredName(request.Name))
            .Apply(RequiredEmail(request.Email))
            .Apply(RequiredAge(request.Age));

    private static Validation<string, string> RequiredName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? Validation.Failure<string, string>("Name is required.")
            : Validation.Return<string, string>(name);

    private static Validation<string, string> RequiredEmail(string? email) =>
        email is null || !email.Contains('@')
            ? Validation.Failure<string, string>("A valid email address is required.")
            : Validation.Return<string, string>(email);

    private static Validation<int, string> RequiredAge(string? age) =>
        int.TryParse(age, out var parsed) && parsed is >= 0 and <= 130
            ? Validation.Return<int, string>(parsed)
            : Validation.Failure<int, string>("Age must be a whole number between 0 and 130.");
}
