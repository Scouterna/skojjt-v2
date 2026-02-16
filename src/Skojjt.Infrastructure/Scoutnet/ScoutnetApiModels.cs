using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Root response from Scoutnet memberlist API.
/// </summary>
public class ScoutnetMemberListResponse
{
    [JsonPropertyName("data")]
    public Dictionary<string, ScoutnetMember> Data { get; set; } = new();

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();
}

/// <summary>
/// A single member from the Scoutnet API.
/// All values come wrapped in a value/raw_value structure.
/// </summary>
public class ScoutnetMember
{
    [JsonPropertyName("member_no")]
    public ScoutnetValue? MemberNo { get; set; }

    [JsonPropertyName("first_name")]
    public ScoutnetValue? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public ScoutnetValue? LastName { get; set; }

    [JsonPropertyName("ssno")]
    public ScoutnetValue? PersonalNumber { get; set; }

    [JsonPropertyName("date_of_birth")]
    public ScoutnetValue? DateOfBirth { get; set; }

    [JsonPropertyName("status")]
    public ScoutnetValue? Status { get; set; }

    [JsonPropertyName("created_at")]
    public ScoutnetValue? CreatedAt { get; set; }

    [JsonPropertyName("confirmed_at")]
    public ScoutnetValue? ConfirmedAt { get; set; }

    [JsonPropertyName("group")]
    public ScoutnetValue? Group { get; set; }

    [JsonPropertyName("unit")]
    public ScoutnetValue? Unit { get; set; }

    [JsonPropertyName("unit_type")]
    public ScoutnetValue? UnitType { get; set; }

    [JsonPropertyName("unit_role")]
    public ScoutnetValue? UnitRole { get; set; }

    [JsonPropertyName("group_role")]
    public ScoutnetValue? GroupRole { get; set; }

    [JsonPropertyName("patrol")]
    public ScoutnetValue? Patrol { get; set; }

    [JsonPropertyName("sex")]
    public ScoutnetValue? Sex { get; set; }

    [JsonPropertyName("address_co")]
    public ScoutnetValue? AddressCo { get; set; }

    [JsonPropertyName("address_1")]
    public ScoutnetValue? Address1 { get; set; }

    [JsonPropertyName("address_2")]
    public ScoutnetValue? Address2 { get; set; }

    [JsonPropertyName("postcode")]
    public ScoutnetValue? Postcode { get; set; }

    [JsonPropertyName("town")]
    public ScoutnetValue? Town { get; set; }

    [JsonPropertyName("country")]
    public ScoutnetValue? Country { get; set; }

    [JsonPropertyName("email")]
    public ScoutnetValue? Email { get; set; }

    [JsonPropertyName("contact_alt_email")]
    public ScoutnetValue? AltEmail { get; set; }

    [JsonPropertyName("contact_mobile_phone")]
    public ScoutnetValue? MobilePhone { get; set; }

    [JsonPropertyName("contact_home_phone")]
    public ScoutnetValue? HomePhone { get; set; }

    [JsonPropertyName("contact_telephone_home")]
    public ScoutnetValue? TelephoneHome { get; set; }

    [JsonPropertyName("contact_mothers_name")]
    public ScoutnetValue? MothersName { get; set; }

    [JsonPropertyName("contact_email_mum")]
    public ScoutnetValue? MothersEmail { get; set; }

    [JsonPropertyName("contact_mobile_mum")]
    public ScoutnetValue? MothersMobile { get; set; }

    [JsonPropertyName("contact_telephone_mum")]
    public ScoutnetValue? MothersTelephone { get; set; }

    [JsonPropertyName("contact_fathers_name")]
    public ScoutnetValue? FathersName { get; set; }

    [JsonPropertyName("contact_email_dad")]
    public ScoutnetValue? FathersEmail { get; set; }

    [JsonPropertyName("contact_mobile_dad")]
    public ScoutnetValue? FathersMobile { get; set; }

    [JsonPropertyName("contact_telephone_dad")]
    public ScoutnetValue? FathersTelephone { get; set; }

    [JsonPropertyName("contact_leader_interest")]
    public ScoutnetValue? LeaderInterest { get; set; }

    [JsonPropertyName("note")]
    public ScoutnetValue? Note { get; set; }

    [JsonPropertyName("nickname")]
    public ScoutnetValue? Nickname { get; set; }

    [JsonPropertyName("roles")]
    public ScoutnetRolesValue? Roles { get; set; }

    // Helper methods to extract values
    public int GetMemberNo() => int.TryParse(MemberNo?.Value, out var result) ? result : 0;
    public string GetFirstName() => FirstName?.Value ?? string.Empty;
    public string GetLastName() => LastName?.Value ?? string.Empty;
    public string? GetPersonalNumber() => PersonalNumber?.Value;
    public string? GetEmail() => Email?.Value;
    public string? GetAltEmail() => AltEmail?.Value;
    public string? GetMobile() => FixCountryPrefix(MobilePhone?.Value);
    public string? GetPhone() => FixCountryPrefix(HomePhone?.Value ?? TelephoneHome?.Value);
    public string? GetStreet() => Address1?.Value;
    public string? GetZipCode() => Postcode?.Value;
    public string? GetZipName() => Town?.Value;
    public string? GetMumName() => MothersName?.Value;
    public string? GetMumEmail() => MothersEmail?.Value;
    public string? GetMumMobile() => FixCountryPrefix(MothersMobile?.Value);
    public string? GetDadName() => FathersName?.Value;
    public string? GetDadEmail() => FathersEmail?.Value;
    public string? GetDadMobile() => FixCountryPrefix(FathersMobile?.Value);
    public string? GetPatrol() => Patrol?.Value;
    public string? GetGroupRole() => GroupRole?.Value;

    public int? GetGroupId() => int.TryParse(Group?.RawValue, out var result) ? result : null;
    public string? GetGroupName() => Group?.Value;

    public int? GetUnitId() => int.TryParse(Unit?.RawValue, out var result) ? result : null;
    public string? GetUnitName() => Unit?.Value;

    public bool IsActive() => Status?.Value == "Aktiv";

    public bool IsLeader()
    {
        var unitRole = UnitRole?.RawValue;
        if (string.IsNullOrEmpty(unitRole)) return false;

        // Leader roles: 2 = Avdelningsledare, 3 = Ledare, 4 = Vice avdelningsledare, 5 = Assisterande ledare
        var leaderRoles = new[] { "2", "3", "4", "5" };
        var roles = unitRole.Split(',').Select(r => r.Trim());
        return roles.Any(r => leaderRoles.Contains(r));
    }

    public DateOnly? GetBirthDate()
    {
        var dateStr = DateOfBirth?.Value;
        if (string.IsNullOrEmpty(dateStr)) return null;
        return DateOnly.TryParse(dateStr, out var result) ? result : null;
    }

    /// <summary>
    /// Fix phone numbers without country prefix.
    /// Scoutnet returns numbers like "46738931758" instead of "+46738931758".
    /// </summary>
    private static string? FixCountryPrefix(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        phone = phone.Trim();
        
        // If it starts with a digit (not 0) and is longer than 8 chars, add +
        if (phone.Length > 8 && phone[0] != '0' && phone[0] != '+' && char.IsDigit(phone[0]))
        {
            return "+" + phone;
        }
        return phone;
    }
}

/// <summary>
/// Generic value wrapper used by Scoutnet API.
/// </summary>
public class ScoutnetValue
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("raw_value")]
    public string? RawValue { get; set; }
}

/// <summary>
/// Special wrapper for the roles field which contains nested objects.
/// The value can be either an empty array [] or an object with group/troop/patrol.
/// </summary>
public class ScoutnetRolesValue
{
    [JsonPropertyName("value")]
    [JsonConverter(typeof(ScoutnetRolesConverter))]
    public ScoutnetRoles? Value { get; set; }
}

/// <summary>
/// Custom JSON converter for ScoutnetRoles that handles both empty arrays and objects.
/// Scoutnet returns "value": [] for members without roles and "value": {...} for members with roles.
/// </summary>
public class ScoutnetRolesConverter : JsonConverter<ScoutnetRoles?>
{
    public override ScoutnetRoles? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle null
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // Handle empty array - Scoutnet returns [] when there are no roles
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Skip the entire array
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;
            }
            return null; // Empty array means no roles
        }

        // Handle object - deserialize normally
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var roles = new ScoutnetRoles();
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "group":
                            roles.Group = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ScoutnetRole>>>(ref reader, options);
                            break;
                        case "troop":
                            roles.Troop = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ScoutnetRole>>>(ref reader, options);
                            break;
                        case "patrol":
                            roles.Patrol = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ScoutnetRole>>>(ref reader, options);
                            break;
                        default:
                            // Skip unknown properties
                            reader.Skip();
                            break;
                    }
                }
            }

            return roles;
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} when parsing ScoutnetRoles");
    }

    public override void Write(Utf8JsonWriter writer, ScoutnetRoles? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>
/// Roles structure containing group and troop roles.
/// </summary>
public class ScoutnetRoles
{
    [JsonPropertyName("group")]
    public Dictionary<string, Dictionary<string, ScoutnetRole>>? Group { get; set; }

    [JsonPropertyName("troop")]
    public Dictionary<string, Dictionary<string, ScoutnetRole>>? Troop { get; set; }

    [JsonPropertyName("patrol")]
    public Dictionary<string, Dictionary<string, ScoutnetRole>>? Patrol { get; set; }
}

/// <summary>
/// Individual role definition.
/// </summary>
public class ScoutnetRole
{
    [JsonPropertyName("role_id")]
    public int RoleId { get; set; }

    [JsonPropertyName("role_key")]
    public string? RoleKey { get; set; }

    [JsonPropertyName("role_name")]
    public string? RoleName { get; set; }
}
