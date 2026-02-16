using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Shared.DTOs;

namespace Skojjt.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingRepository _meetingRepository;
    private readonly ITroopRepository _troopRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MeetingsController(
        IMeetingRepository meetingRepository,
        ITroopRepository troopRepository,
        IUnitOfWork unitOfWork)
    {
        _meetingRepository = meetingRepository;
        _troopRepository = troopRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MeetingSummaryDto>>> GetByTroop([FromQuery] int troopId)
    {
        var meetings = await _meetingRepository.GetByTroopAsync(troopId);
        return Ok(meetings.Select(m => new MeetingSummaryDto(
            m.Id,
            m.TroopId,
            m.MeetingDate,
            m.StartTime,
            m.Name,
            m.DurationMinutes,
            m.IsHike,
            m.Attendances.Count
        )));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MeetingDetailDto>> GetById(int id)
    {
        var meeting = await _meetingRepository.GetWithAttendanceAsync(id);
        if (meeting == null)
            return NotFound();

        var troop = await _troopRepository.GetByIdAsync(meeting.TroopId);

        return Ok(new MeetingDetailDto(
            meeting.Id,
            meeting.TroopId,
            troop?.Name,
            meeting.MeetingDate,
            meeting.StartTime,
            meeting.Name,
            meeting.DurationMinutes,
            meeting.IsHike,
            meeting.Attendances.Select(a => a.PersonId).ToList()
        ));
    }

    [HttpPost]
    public async Task<ActionResult<MeetingDetailDto>> Create([FromBody] MeetingUpsertDto request)
    {
        var meeting = new Meeting
        {
            TroopId = request.TroopId,
            MeetingDate = request.MeetingDate,
            StartTime = request.StartTime,
            Name = request.Name,
            DurationMinutes = request.DurationMinutes,
            IsHike = request.IsHike
        };

        await _meetingRepository.AddAsync(meeting);
        await _unitOfWork.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = meeting.Id },
            new MeetingDetailDto(meeting.Id, meeting.TroopId, null, meeting.MeetingDate, 
                meeting.StartTime, meeting.Name, meeting.DurationMinutes, meeting.IsHike, new List<int>()));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] MeetingUpsertDto request)
    {
        var meeting = await _meetingRepository.GetByIdAsync(id);
        if (meeting == null)
            return NotFound();

        meeting.Name = request.Name;
        meeting.StartTime = request.StartTime;
        meeting.DurationMinutes = request.DurationMinutes;
        meeting.IsHike = request.IsHike;

        await _meetingRepository.UpdateAsync(meeting);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var meeting = await _meetingRepository.GetByIdAsync(id);
        if (meeting == null)
            return NotFound();

        await _meetingRepository.DeleteAsync(meeting);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/attendance")]
    public async Task<ActionResult> UpdateAttendance(int id, [FromBody] AttendanceUpdateDto request)
    {
        var meeting = await _meetingRepository.GetWithAttendanceAsync(id);
        if (meeting == null)
            return NotFound();

        // Clear existing attendance
        meeting.Attendances.Clear();

        // Add new attendance records
        foreach (var personId in request.AttendingPersonIds)
        {
            meeting.Attendances.Add(new MeetingAttendance
            {
                MeetingId = id,
                PersonId = personId
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/attendance/toggle")]
    public async Task<ActionResult> ToggleAttendance(int meetingId, [FromBody] AttendanceToggleDto request)
    {
        var meeting = await _meetingRepository.GetWithAttendanceAsync(meetingId);
        if (meeting == null)
            return NotFound();

        var existing = meeting.Attendances.FirstOrDefault(a => a.PersonId == request.PersonId);

        if (request.IsAttending && existing == null)
        {
            meeting.Attendances.Add(new MeetingAttendance
            {
                MeetingId = meetingId,
                PersonId = request.PersonId
            });
        }
        else if (!request.IsAttending && existing != null)
        {
            meeting.Attendances.Remove(existing);
        }

        await _unitOfWork.SaveChangesAsync();
        return Ok(new { attending = request.IsAttending });
    }
}
