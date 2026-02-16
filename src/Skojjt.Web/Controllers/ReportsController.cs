using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Core.Authentication;
using Skojjt.Core.Exports;
using Skojjt.Core.Interfaces;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Exports;

namespace Skojjt.Web.Controllers;

/// <summary>
/// Error response for export operations.
/// </summary>
public record ExportErrorResponse
{
    /// <summary>
    /// Short error title.
    /// </summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// Detailed error message explaining what went wrong.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// URL to the configuration page where the issue can be fixed.
    /// </summary>
    public string? ConfigurationUrl { get; init; }
}

/// <summary>
/// Controller for generating attendance reports in various formats.
/// </summary>
[ApiController]
[Route("reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IAttendanceExportService _exportService;
    private readonly ILagerbidragExporter _lagerbidragExporter;
    private readonly IMembersCsvExporter _membersCsvExporter;
    private readonly ITroopRepository _troopRepository;
    private readonly IScoutGroupRepository _scoutGroupRepository;
    private readonly ISemesterRepository _semesterRepository;
    private readonly IMeetingRepository _meetingRepository;
    private readonly ICurrentUserService _currentUserService;

    public ReportsController(
        IAttendanceExportService exportService,
        ILagerbidragExporter lagerbidragExporter,
        IMembersCsvExporter membersCsvExporter,
        ITroopRepository troopRepository,
        IScoutGroupRepository scoutGroupRepository,
        ISemesterRepository semesterRepository,
        IMeetingRepository meetingRepository,
        ICurrentUserService currentUserService)
    {
        _exportService = exportService;
        _lagerbidragExporter = lagerbidragExporter;
        _membersCsvExporter = membersCsvExporter;
        _troopRepository = troopRepository;
        _scoutGroupRepository = scoutGroupRepository;
        _semesterRepository = semesterRepository;
        _meetingRepository = meetingRepository;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Export attendance data in DAK XML format.
    /// </summary>
    /// <param name="scoutnetId">Scoutnet troop ID</param>
    /// <param name="semesterId">Semester ID</param>
    [HttpGet("dak")]
    public async Task<IActionResult> ExportDak(
        [FromQuery] int scoutnetId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken)
    {
        return await ExportFormat("dak", scoutnetId, semesterId, cancellationToken);
    }

    /// <summary>
    /// Export attendance data in JSON format.
    /// </summary>
    /// <param name="scoutnetId">Scoutnet troop ID</param>
    /// <param name="semesterId">Semester ID</param>
    [HttpGet("json")]
    public async Task<IActionResult> ExportJson(
        [FromQuery] int scoutnetId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken)
    {
        return await ExportFormat("json", scoutnetId, semesterId, cancellationToken);
    }

    /// <summary>
    /// Export attendance data in Excel format (Gothenburg).
    /// </summary>
    /// <param name="scoutnetId">Scoutnet troop ID</param>
    /// <param name="semesterId">Semester ID</param>
    [HttpGet("excel-gbg")]
    public async Task<IActionResult> ExportExcelGothenburg(
        [FromQuery] int scoutnetId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken)
    {
        return await ExportFormat("excel-gbg", scoutnetId, semesterId, cancellationToken);
    }

    /// <summary>
    /// Export attendance data in Excel format (Stockholm).
    /// </summary>
    /// <param name="scoutnetId">Scoutnet troop ID</param>
    /// <param name="semesterId">Semester ID</param>
    [HttpGet("excel-sthlm")]
    public async Task<IActionResult> ExportExcelStockholm(
        [FromQuery] int scoutnetId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken)
    {
        return await ExportFormat("excel-sthlm", scoutnetId, semesterId, cancellationToken);
    }

    /// <summary>
    /// Export members CSV for Gothenburg municipality attendance grant (aktivitetsbidrag).
    /// </summary>
    /// <param name="scoutGroupId">Scout group ID</param>
    /// <param name="semesterId">Semester ID</param>
    /// <param name="useSemesterMin">If true, use semester minimum meetings; otherwise use year minimum</param>
    [HttpGet("gbg-csv")]
    public async Task<IActionResult> ExportGothenburgCsv(
        [FromQuery] int scoutGroupId,
        [FromQuery] int semesterId,
        [FromQuery] bool useSemesterMin = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify the current user has access to this scout group
            if (!_currentUserService.HasGroupAccess(scoutGroupId))
            {
                return Forbid();
            }

            var input = new GothenburgCsvInput
            {
                ScoutGroupId = scoutGroupId,
                SemesterId = semesterId,
                UseSemesterMinimum = useSemesterMin
            };

            var result = await _membersCsvExporter.ExportAsync(input, cancellationToken);
            return File(result.Data, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ExportErrorResponse
            {
                Error = "Exportfel",
                Message = ex.Message,
                ConfigurationUrl = $"/sk/{scoutGroupId}/settings"
            });
        }
    }

    /// <summary>
    /// Generic export endpoint supporting all formats.
    /// </summary>
    /// <param name="format">Export format (dak, json, excel-gbg, excel-sthlm)</param>
    /// <param name="scoutnetId">Scoutnet troop ID</param>
    /// <param name="semesterId">Semester ID</param>
    [HttpGet("{format}")]
    public async Task<IActionResult> Export(
        string format,
        [FromQuery] int scoutnetId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken)
    {
        return await ExportFormat(format, scoutnetId, semesterId, cancellationToken);
    }

    private async Task<IActionResult> ExportFormat(
        string format,
        int scoutnetId,
        int semesterId,
        CancellationToken cancellationToken)
    {
        // Validate exporter exists
        var exporter = _exportService.GetExporter(format);
        if (exporter == null)
        {
            return BadRequest($"Unknown export format: {format}. Available formats: {string.Join(", ", _exportService.GetExporters().Select(e => e.ExporterId))}");
        }

        // Get troop by ScoutnetId and SemesterId
        var troop = await _troopRepository.GetByScoutnetIdAndSemesterAsync(scoutnetId, semesterId, cancellationToken);
        if (troop == null)
        {
            return NotFound($"Troop not found with ScoutnetId={scoutnetId} and SemesterId={semesterId}");
        }

        // Verify the current user has access to the troop's scout group
        if (!_currentUserService.HasGroupAccess(troop.ScoutGroupId))
        {
            return Forbid();
        }

        // Load related data
        var scoutGroup = await _scoutGroupRepository.GetByIdAsync(troop.ScoutGroupId, cancellationToken);
        if (scoutGroup == null)
        {
            return NotFound($"Scout group not found: {troop.ScoutGroupId}");
        }

        var semester = await _semesterRepository.GetByIdAsync(semesterId, cancellationToken);
        if (semester == null)
        {
            return NotFound($"Semester not found: {semesterId}");
        }

        // Get troop with members
        var troopWithMembers = await _troopRepository.GetWithMembersAsync(troop.Id, cancellationToken);
        if (troopWithMembers == null)
        {
            return NotFound($"Could not load troop members");
        }

        // Get meetings with attendance
        var meetings = await _meetingRepository.GetByTroopWithAttendanceAsync(troop.Id, cancellationToken);

        // Build report data
        var reportData = new AttendanceReportData
        {
            ScoutGroup = scoutGroup,
            Troop = troopWithMembers,
            Semester = semester,
            DefaultLocation = scoutGroup.DefaultCampLocation ?? "Scouthuset",
            IncludeHikeMeetings = scoutGroup.AttendanceInclHike,
            TroopPersons = troopWithMembers.TroopPersons
                .Select(tp => new TroopPersonInfo
                {
                    Person = tp.Person,
                    IsLeader = tp.IsLeader,
                    Patrol = tp.Patrol
                })
                .ToList(),
            Meetings = meetings
                .Select(m => new MeetingInfo
                {
                    Meeting = m,
                    AttendingPersonIds = m.Attendances.Select(a => a.PersonId).ToList()
                })
                .ToList()
        };

        // Generate export - catch configuration/validation errors
        try
        {
            var result = await _exportService.ExportAsync(format, reportData, cancellationToken);
            return File(result.Data, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            // Return a user-friendly error for configuration issues
            // (e.g., missing AssociationId, MunicipalityId, etc.)
            return BadRequest(new ExportErrorResponse
            {
                Error = "Exportfel",
                Message = ex.Message,
                ConfigurationUrl = $"/scout-groups/{scoutGroup.Id}/settings"
            });
        }
    }

    /// <summary>
    /// Export lagerbidrag (camp subsidy) report for Gothenburg.
    /// </summary>
    [HttpGet("lagerbidrag-gbg")]
    public async Task<IActionResult> ExportLagerbidragGothenburg(
        [FromQuery] int scoutGroupId,
        [FromQuery] int semesterId,
        [FromQuery] int? troopId,
        [FromQuery] string contact,
        [FromQuery] string? contactEmail,
        [FromQuery] string? contactPhone,
        [FromQuery] string site,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] bool duringBreak,
        CancellationToken cancellationToken)
    {
        return await ExportLagerbidrag("gbg", scoutGroupId, semesterId, troopId,
            contact, contactEmail, contactPhone, site, fromDate, toDate, duringBreak, cancellationToken);
    }

    /// <summary>
    /// Export lagerbidrag (camp subsidy) report for Stockholm.
    /// </summary>
    [HttpGet("lagerbidrag-sthlm")]
    public async Task<IActionResult> ExportLagerbidragStockholm(
        [FromQuery] int scoutGroupId,
        [FromQuery] int semesterId,
        [FromQuery] int? troopId,
        [FromQuery] string contact,
        [FromQuery] string? contactEmail,
        [FromQuery] string? contactPhone,
        [FromQuery] string site,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] bool duringBreak,
        CancellationToken cancellationToken)
    {
        return await ExportLagerbidrag("sthlm", scoutGroupId, semesterId, troopId,
            contact, contactEmail, contactPhone, site, fromDate, toDate, duringBreak, cancellationToken);
    }

    private async Task<IActionResult> ExportLagerbidrag(
        string region,
        int scoutGroupId,
        int semesterId,
        int? troopId,
        string contact,
        string? contactEmail,
        string? contactPhone,
        string site,
        DateOnly fromDate,
        DateOnly toDate,
        bool duringBreak,
        CancellationToken cancellationToken)
    {
        // Load scout group
        var scoutGroup = await _scoutGroupRepository.GetByIdAsync(scoutGroupId, cancellationToken);
        if (scoutGroup == null)
        {
            return NotFound($"Scout group not found: {scoutGroupId}");
        }

        // Verify the current user has access to this scout group
        if (!_currentUserService.HasGroupAccess(scoutGroupId))
        {
            return Forbid();
        }

        // Load semester
        var semester = await _semesterRepository.GetByIdAsync(semesterId, cancellationToken);
        if (semester == null)
        {
            return NotFound($"Semester not found: {semesterId}");
        }

        // Load troops (either specific troop or all troops in the semester)
        List<Core.Entities.Troop> troops;
        if (troopId.HasValue)
        {
            var troop = await _troopRepository.GetWithMembersAsync(troopId.Value, cancellationToken);
            if (troop == null)
            {
                return NotFound($"Troop not found: {troopId.Value}");
            }
            troops = [troop];
        }
        else
        {
            troops = (await _troopRepository.GetByScoutGroupAndSemesterWithMembersAsync(scoutGroupId, semesterId, cancellationToken)).ToList();
        }

        if (troops.Count == 0)
        {
            return BadRequest("No troops found for the specified criteria");
        }

        // Aggregate all troop persons and meetings
        var allTroopPersons = new List<TroopPersonInfo>();
        var allMeetings = new List<MeetingInfo>();

        foreach (var troop in troops)
        {
            // Add troop persons (avoiding duplicates by person ID)
            foreach (var tp in troop.TroopPersons)
            {
                if (!allTroopPersons.Any(existing => existing.Person.Id == tp.Person.Id))
                {
                    allTroopPersons.Add(new TroopPersonInfo
                    {
                        Person = tp.Person,
                        IsLeader = tp.IsLeader,
                        Patrol = tp.Patrol
                    });
                }
            }

            // Load meetings with attendance for this troop
            var meetings = await _meetingRepository.GetByTroopWithAttendanceAsync(troop.Id, cancellationToken);
            allMeetings.AddRange(meetings.Select(m => new MeetingInfo
            {
                Meeting = m,
                AttendingPersonIds = m.Attendances.Select(a => a.PersonId).ToList()
            }));
        }

        // Use first troop for the report (or create a combined name)
        var primaryTroop = troops.First();

        // Build attendance report data
        var attendanceData = new AttendanceReportData
        {
            ScoutGroup = scoutGroup,
            Troop = primaryTroop,
            Semester = semester,
            DefaultLocation = scoutGroup.DefaultCampLocation ?? "Scouthuset",
            IncludeHikeMeetings = true, // Lagerbidrag only uses hike meetings
            TroopPersons = allTroopPersons,
            Meetings = allMeetings
        };

        // Build lagerbidrag input
        var input = new LagerbidragInput
        {
            AttendanceData = attendanceData,
            ContactPerson = contact,
            ContactEmail = contactEmail ?? string.Empty,
            ContactPhone = contactPhone ?? string.Empty,
            Site = site,
            DateFrom = fromDate,
            DateTo = toDate,
            HikeDuringBreak = duringBreak,
            Region = region
        };

        try
        {
            var result = await _lagerbidragExporter.ExportAsync(input, cancellationToken);
            return File(result.Data, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get list of available export formats.
    /// </summary>
    [HttpGet("formats")]
    public IActionResult GetFormats()
    {
        var formats = _exportService.GetExporters()
            .Select(e => new { id = e.ExporterId, name = e.DisplayName })
            .ToList();

        return Ok(formats);
    }
}
