namespace Example.InterviewQuestion4.Api.Contracts;

public sealed record PersonResponse(int Id, string FirstName, string LastName, string Email,
    string Phone, string ProfileFileName, string ProfileContentType, DateOnly BirthDate,
    string Occupation, string Sex, DateTime CreatedAt);
