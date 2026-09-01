namespace CortanaLib.Contracts;

public sealed record MessageResponse(string Message);

public sealed record ProblemResponse(string Title, int Status, string Detail, string? Instance);
