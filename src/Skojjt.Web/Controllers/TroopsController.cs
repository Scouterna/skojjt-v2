using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Shared.DTOs;

namespace Skojjt.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TroopsController : ControllerBase
{
    private readonly ITroopRepository _troopRepository;
    private readonly IMeetingRepository _meetingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TroopsController(
        ITroopRepository troopRepository, 
        IMeetingRepository meetingRepository,
        IUnitOfWork unitOfWork)
    {
        _troopRepository = troopRepository;
        _meetingRepository = meetingRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TroopSummaryDto>>> GetAll(
        [FromQuery] int? scoutGroupId, 
        [FromQuery] int? semesterId)
    {
        IReadOnlyList<Troop> troops;
        
        if (scoutGroupId.HasValue && semesterId.HasValue)
        {
            troops = await _troopRepository.GetByScoutGroupAndSemesterAsync(scoutGroupId.Value, semesterId.Value);
        }
        else if (scoutGroupId.HasValue)
        {
            troops = await _troopRepository.GetByScoutGroupAsync(scoutGroupId.Value);
        }
        else
        {
            troops = await _troopRepository.GetAllAsync();
        }

        return Ok(troops.Select(t => new TroopSummaryDto(
            t.Id,
            t.ScoutnetId,
            t.Name,
            t.SemesterId,
            t.Semester?.DisplayName,
            t.TroopPersons.Count
        )));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TroopDetailDto>> GetById(int id)
    {
        var troop = await _troopRepository.GetWithMembersAsync(id);
        if (troop == null)
            return NotFound();

        var meetings = await _meetingRepository.GetByTroopAsync(id);

        return Ok(new TroopDetailDto(
            troop.Id,
            troop.ScoutnetId,
            troop.ScoutGroupId,
            troop.SemesterId,
            troop.Name,
            troop.DefaultStartTime,
            troop.DefaultDurationMinutes,
            troop.IsLocked,
            troop.TroopPersons.Select(tp => new TroopMemberDto(
                tp.PersonId,
                tp.Person.FullName,
                tp.Person.Age,
                tp.Patrol,
                tp.IsLeader
            )).ToList(),
            meetings.Select(m => new MeetingSummaryDto(
                m.Id,
                m.TroopId,
                m.MeetingDate,
                m.StartTime,
                m.Name,
                m.DurationMinutes,
                m.IsHike,
                m.Attendances.Count
            )).ToList()
        ));
    }

    [HttpPost]
    public async Task<ActionResult<TroopSummaryDto>> Create([FromBody] TroopCreateDto request, [FromQuery] int scoutGroupId)
    {
        var scoutnetId = request.ScoutnetId;

        // Local troop: allocate an ID from the scout group's reserved range.
        // Uses a transaction so the NextLocalTroopId increment is atomic with troop creation.
        if (scoutnetId <= 0)
        {
            var scoutGroup = await _unitOfWork.ScoutGroups.GetByIdAsync(scoutGroupId);
            if (scoutGroup == null)
                return NotFound($"Scout group {scoutGroupId} not found");

            if (scoutGroup.NextLocalTroopId > 1000)
                return Conflict("Local troop ID range (250-1000) exhausted for this scout group");

            scoutnetId = scoutGroup.NextLocalTroopId;
            scoutGroup.NextLocalTroopId++;
            await _unitOfWork.ScoutGroups.UpdateAsync(scoutGroup);
        }

        var troop = new Troop
        {
            ScoutnetId = scoutnetId,
            ScoutGroupId = scoutGroupId,
            SemesterId = request.SemesterId,
            Name = request.Name,
            DefaultStartTime = request.DefaultStartTime ?? new TimeOnly(18, 30),
            DefaultDurationMinutes = request.DefaultDurationMinutes ?? 90
        };

        await _troopRepository.AddAsync(troop);
        await _unitOfWork.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = troop.Id },
            new TroopSummaryDto(troop.Id, troop.ScoutnetId, troop.Name, troop.SemesterId, null, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, [FromBody] TroopCreateDto request)
    {
        var troop = await _troopRepository.GetByIdAsync(id);
        if (troop == null)
            return NotFound();

        troop.Name = request.Name;
        if (request.DefaultStartTime.HasValue)
            troop.DefaultStartTime = request.DefaultStartTime.Value;
        if (request.DefaultDurationMinutes.HasValue)
            troop.DefaultDurationMinutes = request.DefaultDurationMinutes.Value;

        await _troopRepository.UpdateAsync(troop);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:int}/members/{personId:int}")]
    public async Task<ActionResult> AddMember(int id, int personId, [FromQuery] bool isLeader = false)
    {
        var troop = await _troopRepository.GetWithMembersAsync(id);
        if (troop == null)
            return NotFound();

        if (troop.TroopPersons.Any(tp => tp.PersonId == personId))
            return Conflict("Person is already a member of this troop");

        troop.TroopPersons.Add(new TroopPerson
        {
            TroopId = id,
            PersonId = personId,
            IsLeader = isLeader
        });

        await _unitOfWork.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}/members/{personId:int}")]
    public async Task<ActionResult> RemoveMember(int id, int personId)
    {
        var troop = await _troopRepository.GetWithMembersAsync(id);
        if (troop == null)
            return NotFound();

        var member = troop.TroopPersons.FirstOrDefault(tp => tp.PersonId == personId);
        if (member == null)
            return NotFound("Person is not a member of this troop");

        troop.TroopPersons.Remove(member);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }
}
