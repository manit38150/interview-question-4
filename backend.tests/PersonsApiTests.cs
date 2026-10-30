using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Example.InterviewQuestion4.Api.Contracts;
using Example.InterviewQuestion4.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StbImageWriteSharp;

namespace Example.InterviewQuestion4.Api.Tests;

public sealed class PersonsApiTests
{
    private static Dictionary<string, object?> ValidRequest(bool jpeg = false)
    {
        using var stream = new MemoryStream();
        var writer = new ImageWriter();
        if (jpeg) writer.WriteJpg(new byte[] { 255, 0, 0 }, 1, 1, ColorComponents.RedGreenBlue, stream, 90);
        else writer.WritePng(new byte[] { 255, 0, 0 }, 1, 1, ColorComponents.RedGreenBlue, stream);
        return new()
        {
            ["firstName"] = "Somchai", ["lastName"] = "Jaidee",
            ["email"] = "somchai@example.com", ["phone"] = "0812345678",
            ["profileBase64"] = Convert.ToBase64String(stream.ToArray()),
            ["profileFileName"] = jpeg ? "profile.jpg" : "profile.png",
            ["profileContentType"] = jpeg ? "image/jpeg" : "image/png",
            ["birthDate"] = "2000-02-29", ["occupation"] = "Developer", ["sex"] = "Male"
        };
    }

    [Theory]
    [InlineData(false, "0812345678")]
    [InlineData(true, "021234567")]
    public async Task Create_PersistsPersonAndReturnsDatabaseGeneratedId(bool jpeg, string phone)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = ValidRequest(jpeg);
        request["phone"] = phone;
        // The client cannot choose server-owned fields.
        request["id"] = 999;
        request["createdAt"] = "1900-01-01T00:00:00Z";
        var before = DateTime.UtcNow;
        var response = await client.PostAsJsonAsync("/api/persons", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var person = await response.Content.ReadFromJsonAsync<PersonResponse>();
        Assert.NotNull(person);
        Assert.True(person.Id > 0);
        Assert.NotEqual(999, person.Id);
        Assert.InRange(person.CreatedAt, before, DateTime.UtcNow);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Persons.AsNoTracking().SingleAsync();
        Assert.Equal(person.Id, saved.Id);
        Assert.Equal("Somchai", saved.FirstName);
        Assert.Equal("Jaidee", saved.LastName);
        Assert.Equal("somchai@example.com", saved.Email);
        Assert.Equal(phone, saved.Phone);
        Assert.Equal(request["profileBase64"], saved.ProfileBase64);
        Assert.Equal(request["profileFileName"], saved.ProfileFileName);
        Assert.Equal(request["profileContentType"], saved.ProfileContentType);
        Assert.Equal(new DateOnly(2000, 2, 29), saved.BirthDate);
        Assert.Equal("Developer", saved.Occupation);
        Assert.Equal("Male", saved.Sex);
        var second = await client.PostAsJsonAsync("/api/persons", request);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True((await second.Content.ReadFromJsonAsync<PersonResponse>())!.Id > person.Id);
    }

    public static IEnumerable<object?[]> InvalidFields()
    {
        foreach (var field in ValidRequest().Keys)
        {
            yield return [field, null];
            yield return [field, "   "];
        }
        yield return ["email", "not-an-email"];
        foreach (var phone in new[] { "123456789", "+66812345678", "081-234-5678", "08123456", "08123456789", "๐๘๑๒๓๔๕๖๗๘" })
            yield return ["phone", phone];
        foreach (var date in new[] { "2001-02-29", "2000-13-01", "29/02/2000", "2569-01-01", "0000-01-01", "2099-01-01", "2000-01-01T00:00:00Z" })
            yield return ["birthDate", date];
        yield return ["birthDate", DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")];
        yield return ["profileBase64", "not base64!"];
        yield return ["profileBase64", Convert.ToBase64String("not an image"u8.ToArray())];
        yield return ["profileBase64", "iVBORw0KGgo="]; // PNG header without image data
        yield return ["profileBase64", "/9j/2Q=="]; // JPEG markers without image data
        yield return ["profileBase64", "R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw=="]; // GIF disguised as PNG
        yield return ["profileFileName", "profile.exe"];
        yield return ["profileContentType", "image/jpeg"]; // mismatched PNG
        yield return ["profileContentType", "image/gif"];
        yield return ["firstName", new string('a', 101)];
        var oversizedDimensions = Convert.FromBase64String((string)ValidRequest()["profileBase64"]!);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(oversizedDimensions.AsSpan(16, 4), 100_000);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(oversizedDimensions.AsSpan(20, 4), 100_000);
        yield return ["profileBase64", Convert.ToBase64String(oversizedDimensions)];
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Create_InvalidFieldReturns400AndDoesNotPersist(string field, object? value)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = ValidRequest();
        request[field] = value;
        var response = await client.PostAsJsonAsync("/api/persons", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").EnumerateObject().Any());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Persons.CountAsync());
    }

    [Theory]
    [InlineData(0, HttpStatusCode.Created)]
    [InlineData(1, HttpStatusCode.BadRequest)]
    [InlineData(3, HttpStatusCode.BadRequest)]
    public async Task Create_EnforcesDecodedImageSize(int extraBytes, HttpStatusCode expected)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = ValidRequest();
        var bytes = Convert.FromBase64String((string)request["profileBase64"]!);
        Array.Resize(ref bytes, CreatePersonRequest.MaxImageBytes + extraBytes);
        request["profileBase64"] = Convert.ToBase64String(bytes);
        var response = await client.PostAsJsonAsync("/api/persons", request);
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{ invalid json")]
    [InlineData("{\"birthDate\":123}")]
    public async Task Create_InvalidBodyReturns400(string json)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/persons", content)).StatusCode);
    }

    [Fact]
    public async Task Create_DatabaseFailureReturnsGeneric500()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync("DROP TABLE Persons");
        var response = await client.PostAsJsonAsync("/api/persons", ValidRequest());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unable to save person", body);
        Assert.DoesNotContain("Sqlite", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Persons", body);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Migration_CanRollBackAndReapply()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("0");
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        await db.Database.MigrateAsync();
        Assert.Equal(0, await db.Persons.CountAsync());
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                connection.Open();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            });
        }

        protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }
}
