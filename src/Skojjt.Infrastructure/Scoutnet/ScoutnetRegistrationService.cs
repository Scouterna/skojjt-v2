using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Core.Utilities;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Service for registering new members on the Scoutnet waiting list.
/// Handles validation, form data construction, API communication,
/// and local database persistence.
/// </summary>
public class ScoutnetRegistrationService : IScoutnetRegistrationService
{
    private readonly IScoutGroupRepository _scoutGroupRepository;
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly IScoutnetApiClient _apiClient;
    private readonly ILogger<ScoutnetRegistrationService> _logger;

    public ScoutnetRegistrationService(
        IScoutGroupRepository scoutGroupRepository,
        IDbContextFactory<SkojjtDbContext> contextFactory,
        IScoutnetApiClient apiClient,
        ILogger<ScoutnetRegistrationService> logger)
    {
        _scoutGroupRepository = scoutGroupRepository;
        _contextFactory = contextFactory;
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<WaitinglistRegistrationResult> AddToWaitinglistAsync(
        int scoutGroupId,
        WaitinglistRegistrationRequest request,
        int? troopId = null,
        CancellationToken cancellationToken = default)
    {
        // Validate required fields
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return new WaitinglistRegistrationResult
            {
                Success = false,
                ErrorMessage = validationError
            };
        }

        // Look up scout group to get the API key
        var scoutGroup = await _scoutGroupRepository.GetByIdAsync(scoutGroupId, cancellationToken);
        if (scoutGroup == null)
        {
            return new WaitinglistRegistrationResult
            {
                Success = false,
                ErrorMessage = "Scoutkåren kunde inte hittas."
            };
        }

        if (string.IsNullOrEmpty(scoutGroup.ApiKeyWaitinglist))
        {
            return new WaitinglistRegistrationResult
            {
                Success = false,
                ErrorMessage = "API-nyckel för kölistan är inte konfigurerad för denna scoutkår."
            };
        }

        // Build form data and call the API
        var formData = BuildFormData(request);

        _logger.LogInformation("Adding {FirstName} {LastName} to waiting list for group {GroupId}",
            request.FirstName, request.LastName, scoutGroupId);

        try
        {
            var result = await _apiClient.RegisterMemberAsync(
                scoutGroupId, scoutGroup.ApiKeyWaitinglist, formData, cancellationToken);

            if (result.Success && result.MemberNo > 0)
            {
                _logger.LogInformation("Successfully added person with member_no {MemberNo} to waiting list",
                    result.MemberNo);

                await CreatePersonInDatabaseAsync(scoutGroupId, result.MemberNo, request, troopId, cancellationToken);
            }
            else if (result.Success)
            {
                _logger.LogWarning("Person added to waiting list but no member_no returned in response");
            }

            return result;
        }
        catch (ScoutnetApiException ex)
        {
            _logger.LogError(ex, "Scoutnet API error while adding person to waiting list for group {GroupId}",
                scoutGroupId);
            return new WaitinglistRegistrationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Validates the registration request and returns an error message if invalid, or null if valid.
    /// </summary>
    internal static string? ValidateRequest(WaitinglistRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return "Förnamn måste anges.";

        if (string.IsNullOrWhiteSpace(request.LastName))
            return "Efternamn måste anges.";

        if (string.IsNullOrWhiteSpace(request.Personnummer))
            return "Personnummer måste anges.";

        var pnr = new Personnummer(request.Personnummer);
        if (!pnr.IsValid)
            return "Personnumret är ogiltigt. Ange i formatet YYYYMMDD-NNNN.";

        if (string.IsNullOrWhiteSpace(request.Email))
            return "E-postadress måste anges.";

        if (string.IsNullOrWhiteSpace(request.AddressLine1))
            return "Adress måste anges.";

        if (string.IsNullOrWhiteSpace(request.ZipCode))
            return "Postnummer måste anges.";

        if (string.IsNullOrWhiteSpace(request.ZipName))
            return "Postort måste anges.";

        return null;
    }

    /// <summary>
    /// Builds the form data dictionary for the Scoutnet registration API.
    /// Mirrors the v1 Python AddPersonToWaitinglist function.
    /// </summary>
    internal static Dictionary<string, string> BuildFormData(WaitinglistRegistrationRequest request)
    {
        var pnr = new Personnummer(request.Personnummer);
        var form = new Dictionary<string, string>
        {
            // Profile
            ["profile[first_name]"] = request.FirstName,
            ["profile[last_name]"] = request.LastName,
            ["profile[ssno]"] = pnr.ToString(),
            ["profile[email]"] = request.Email,
            ["profile[date_of_birth]"] = pnr.BirthDayString,
            ["profile[sex]"] = pnr.IsMale ? "1" : "2",
            ["profile[preferred_culture]"] = "sv",
            ["profile[newsletter]"] = "1",

            // Address
            ["address_list[addresses][address_1][address_line1]"] = request.AddressLine1,
            ["address_list[addresses][address_1][zip_code]"] = request.ZipCode,
            ["address_list[addresses][address_1][zip_name]"] = request.ZipName,
            ["address_list[addresses][address_1][address_type]"] = "0",
            ["address_list[addresses][address_1][country_code]"] = "752",
            ["address_list[addresses][address_1][is_primary]"] = "1",

            // Membership
            ["membership[status]"] = "1"
        };

        // Contacts — use the same indices as v1 to match Scoutnet API expectations
        AddContact(form, 1, ScoutnetContactFields.Mobiltelefon, request.Mobile);
        AddContact(form, 2, ScoutnetContactFields.Hemtelefon, request.Phone);
        AddContact(form, 3, ScoutnetContactFields.Anhorig1Namn, request.Guardian1Name);
        AddContact(form, 4, ScoutnetContactFields.Anhorig1Epost, request.Guardian1Email);
        AddContact(form, 5, ScoutnetContactFields.Anhorig1Mobiltelefon, request.Guardian1Mobile);
        AddContact(form, 6, ScoutnetContactFields.Anhorig1Hemtelefon, request.Guardian1Phone);
        AddContact(form, 7, ScoutnetContactFields.Anhorig2Namn, request.Guardian2Name);
        AddContact(form, 8, ScoutnetContactFields.Anhorig2Epost, request.Guardian2Email);
        AddContact(form, 9, ScoutnetContactFields.Anhorig2Mobiltelefon, request.Guardian2Mobile);
        AddContact(form, 10, ScoutnetContactFields.Anhorig2Hemtelefon, request.Guardian2Phone);

        return form;
    }

    private static void AddContact(Dictionary<string, string> form, int index, int contactTypeId, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        form[$"contact_list[contacts][contact_{index}][details]"] = value;
        form[$"contact_list[contacts][contact_{index}][contact_type_id]"] = contactTypeId.ToString();
    }

    /// <summary>
    /// Creates the Person, ScoutGroupPerson, and optionally TroopPerson records
    /// in the local database after a successful Scoutnet registration.
    /// </summary>
    private async Task CreatePersonInDatabaseAsync(
        int scoutGroupId,
        int memberNo,
        WaitinglistRegistrationRequest request,
        int? troopId,
        CancellationToken cancellationToken)
    {
        await using var context = _contextFactory.CreateDbContext();

        var pnr = new Personnummer(request.Personnummer);
        var person = new Person
        {
            Id = memberNo,
            FirstName = request.FirstName,
            LastName = request.LastName,
            BirthDate = pnr.BirthDay,
            PersonalNumber = pnr,
            Email = request.Email,
            Phone = request.Phone,
            Mobile = request.Mobile,
            Street = request.AddressLine1,
            ZipCode = request.ZipCode,
            ZipName = request.ZipName,
            MumName = request.Guardian1Name,
            MumEmail = request.Guardian1Email,
            MumMobile = request.Guardian1Mobile,
            DadName = request.Guardian2Name,
            DadEmail = request.Guardian2Email,
            DadMobile = request.Guardian2Mobile,
            Removed = false
        };

        context.Persons.Add(person);

        context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = memberNo,
            ScoutGroupId = scoutGroupId,
            NotInScoutnet = false
        });

        if (troopId.HasValue)
        {
            context.TroopPersons.Add(new TroopPerson
            {
                TroopId = troopId.Value,
                PersonId = memberNo,
                IsLeader = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created Person {MemberNo} ({FullName}) in database for group {GroupId}{TroopInfo}",
            memberNo, $"{request.FirstName} {request.LastName}", scoutGroupId,
            troopId.HasValue ? $", added to troop {troopId.Value}" : string.Empty);
    }
}
