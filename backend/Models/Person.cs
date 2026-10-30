namespace Example.InterviewQuestion4.Api.Models;

public sealed class Person
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }
    public required string ProfileBase64 { get; set; }
    public required string ProfileFileName { get; set; }
    public required string ProfileContentType { get; set; }
    public DateOnly BirthDate { get; set; }
    public required string Occupation { get; set; }
    public required string Sex { get; set; }
    public DateTime CreatedAt { get; set; }
}
