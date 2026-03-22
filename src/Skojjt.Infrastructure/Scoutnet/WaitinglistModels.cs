namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Request to register a new member on the Scoutnet waiting list.
/// </summary>
public class WaitinglistRegistrationRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Full Swedish personal identity number (personnummer) in YYYYMMDDNNNN or YYYYMMDD-NNNN format.
    /// </summary>
    public string Personnummer { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string ZipCode { get; set; } = string.Empty;

    public string ZipName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Mobile { get; set; }

    /// <summary>Anhörig 1 - namn</summary>
    public string? Guardian1Name { get; set; }

    /// <summary>Anhörig 1 - e-post</summary>
    public string? Guardian1Email { get; set; }

    /// <summary>Anhörig 1 - mobiltelefon</summary>
    public string? Guardian1Mobile { get; set; }

    /// <summary>Anhörig 1 - hemtelefon</summary>
    public string? Guardian1Phone { get; set; }

    /// <summary>Anhörig 2 - namn</summary>
    public string? Guardian2Name { get; set; }

    /// <summary>Anhörig 2 - e-post</summary>
    public string? Guardian2Email { get; set; }

    /// <summary>Anhörig 2 - mobiltelefon</summary>
    public string? Guardian2Mobile { get; set; }

    /// <summary>Anhörig 2 - hemtelefon</summary>
    public string? Guardian2Phone { get; set; }
}

/// <summary>
/// Result from a Scoutnet waiting list registration.
/// </summary>
public class WaitinglistRegistrationResult
{
    public bool Success { get; set; }

    /// <summary>
    /// The Scoutnet member number assigned to the new member (0 if unknown).
    /// </summary>
    public int MemberNo { get; set; }

    /// <summary>
    /// Error message if registration failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Scoutnet contact field type IDs used in the registration API.
/// </summary>
internal static class ScoutnetContactFields
{
    public const int Mobiltelefon = 1;
    public const int Hemtelefon = 2;
    public const int Anhorig1Namn = 14;
    public const int Anhorig1Epost = 33;
    public const int Anhorig1Mobiltelefon = 38;
    public const int Anhorig1Hemtelefon = 43;
    public const int Anhorig2Namn = 16;
    public const int Anhorig2Epost = 34;
    public const int Anhorig2Mobiltelefon = 39;
    public const int Anhorig2Hemtelefon = 44;
}
