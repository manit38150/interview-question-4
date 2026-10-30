using System.Globalization;
using Example.InterviewQuestion4.Api.Contracts;
using Example.InterviewQuestion4.Api.Data;
using Example.InterviewQuestion4.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Example.InterviewQuestion4.Api.Controllers;

[ApiController]
[Route("api/persons")]
public sealed class PersonsController(AppDbContext db, ILogger<PersonsController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PersonResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PersonResponse>> Create(CreatePersonRequest request, CancellationToken cancellationToken)
    {
        var person = new Person
        {
            FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
            Email = request.Email.Trim(), Phone = request.Phone,
            ProfileBase64 = request.ProfileBase64, ProfileFileName = request.ProfileFileName,
            ProfileContentType = request.ProfileContentType,
            BirthDate = DateOnly.ParseExact(request.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Occupation = request.Occupation.Trim(), Sex = request.Sex.Trim(), CreatedAt = DateTime.UtcNow
        };
        db.Persons.Add(person);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            logger.LogError("Person persistence failed. TraceId: {TraceId}; ErrorType: {ErrorType}",
                HttpContext.TraceIdentifier, ex.GetType().Name);
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Unable to save person. Please try again later.");
        }

        return StatusCode(StatusCodes.Status201Created, new PersonResponse(person.Id,
            person.FirstName, person.LastName, person.Email, person.Phone,
            person.ProfileFileName, person.ProfileContentType, person.BirthDate,
            person.Occupation, person.Sex, person.CreatedAt));
    }
}
