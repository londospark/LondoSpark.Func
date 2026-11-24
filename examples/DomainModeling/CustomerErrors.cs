using LondoSpark.Func;

namespace Examples.DomainModeling;

/// <summary>Every way a registration can be refused, so a pipeline can carry one failure type.</summary>
public readonly union CustomerError(InvalidRequest, AlreadyRegistered);

/// <summary>The field problems found in a request, gathered instead of stopping at the first.</summary>
/// <param name="Errors">Every field error, in the order the fields were checked.</param>
public sealed record InvalidRequest(NonEmptyList<ValidationError> Errors);

/// <summary>The requested email already belongs to a registered customer.</summary>
/// <param name="Email">The email address that is already registered.</param>
public sealed record AlreadyRegistered(string Email);
