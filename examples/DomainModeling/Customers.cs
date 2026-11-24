namespace Examples.DomainModeling;

/// <summary>A registration request as it arrives from a boundary such as HTTP or a command line.</summary>
/// <param name="Name">The customer name, if supplied.</param>
/// <param name="Email">The email address, if supplied.</param>
/// <param name="Age">The age as text, if supplied.</param>
public readonly record struct CustomerRequest(string? Name, string? Email, string? Age);

/// <summary>A request that passed validation but has no identity yet.</summary>
/// <param name="Name">The validated customer name.</param>
/// <param name="Email">The validated email address.</param>
/// <param name="Age">The validated age.</param>
public readonly record struct NewCustomer(string Name, string Email, int Age);

/// <summary>A registered customer.</summary>
/// <param name="Id">The assigned identity.</param>
/// <param name="Name">The customer name.</param>
/// <param name="Email">The email address.</param>
/// <param name="Age">The age.</param>
public readonly record struct Customer(int Id, string Name, string Email, int Age);
