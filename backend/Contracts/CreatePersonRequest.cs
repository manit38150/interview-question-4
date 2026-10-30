using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Example.InterviewQuestion4.Api.Contracts;

public sealed class CreatePersonRequest : IValidatableObject
{
    public const int MaxImageBytes = 2 * 1024 * 1024;
    public const int MaxBase64Length = ((MaxImageBytes + 2) / 3) * 4;

    [Required, StringLength(100)] public string FirstName { get; init; } = "";
    [Required, StringLength(100)] public string LastName { get; init; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; init; } = "";
    [Required, RegularExpression(@"0[0-9]{8,9}", ErrorMessage = "Phone must be a Thai local number with 9-10 digits starting with 0.")]
    public string Phone { get; init; } = "";
    [Required, StringLength(MaxBase64Length)] public string ProfileBase64 { get; init; } = "";
    [Required, StringLength(255)] public string ProfileFileName { get; init; } = "";
    [Required, RegularExpression("image/(jpeg|png)")] public string ProfileContentType { get; init; } = "";
    [Required] public string BirthDate { get; init; } = "";
    [Required, StringLength(100)] public string Occupation { get; init; } = "";
    [Required, StringLength(50)] public string Sex { get; init; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!DateOnly.TryParseExact(BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var birthday) || birthday > DateOnly.FromDateTime(DateTime.UtcNow))
            yield return new ValidationResult("BirthDate must be a valid Gregorian date in yyyy-MM-dd format and cannot be in the future.", [nameof(BirthDate)]);

        if (string.IsNullOrWhiteSpace(ProfileBase64) || ProfileBase64.Length > MaxBase64Length)
            yield break;

        var bytes = new byte[MaxImageBytes + 2];
        if (!Convert.TryFromBase64String(ProfileBase64, bytes, out var count))
        {
            yield return new ValidationResult("ProfileBase64 must contain valid raw Base64.", [nameof(ProfileBase64)]);
            yield break;
        }
        if (count > MaxImageBytes)
        {
            yield return new ValidationResult("Profile image must not exceed 2 MB.", [nameof(ProfileBase64)]);
            yield break;
        }

        var extension = Path.GetExtension(ProfileFileName ?? "").ToLowerInvariant();
        var png = count >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = count >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        var valid = (png && ProfileContentType == "image/png" && extension == ".png") ||
            (jpeg && ProfileContentType == "image/jpeg" && extension is ".jpg" or ".jpeg");
        if (valid)
        {
            try
            {
                using var stream = new MemoryStream(bytes, 0, count);
                var info = StbImageSharp.ImageInfo.FromStream(stream);
                // Bound decoded memory even when a highly compressed file is below 2 MB.
                valid = info is { Width: > 0, Height: > 0 } && (long)info.Value.Width * info.Value.Height <= 16_000_000;
                if (valid)
                {
                    stream.Position = 0;
                    _ = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The decoder throws Exception for malformed images; never expose decoder details.
                valid = false;
            }
        }
        if (!valid)
            yield return new ValidationResult("Profile must be a valid JPG/PNG image of at most 16 million pixels matching its filename and content type.",
                [nameof(ProfileBase64), nameof(ProfileFileName), nameof(ProfileContentType)]);
    }
}
