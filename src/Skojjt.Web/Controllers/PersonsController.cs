using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Core.Interfaces;
using Skojjt.Shared.DTOs;

namespace Skojjt.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PersonsController : ControllerBase
{
    private readonly IPersonRepository _personRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PersonsController(IPersonRepository personRepository, IUnitOfWork unitOfWork)
    {
        _personRepository = personRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonDetailDto>>> GetByScoutGroup(
        [FromQuery] int scoutGroupId,
        [FromQuery] bool includeRemoved = false)
    {
        var persons = includeRemoved 
            ? await _personRepository.GetByScoutGroupAsync(scoutGroupId)
            : await _personRepository.GetActiveByScoutGroupAsync(scoutGroupId);

        return Ok(persons.Select(MapToDetailDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PersonDetailDto>> GetById(int id)
    {
        var person = await _personRepository.GetWithTroopsAsync(id);
        if (person == null)
            return NotFound();

        return Ok(MapToDetailDto(person));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<PersonSummaryDto>>> Search(
        [FromQuery] int scoutGroupId,
        [FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Ok(Enumerable.Empty<PersonSummaryDto>());

        var persons = await _personRepository.SearchByNameAsync(scoutGroupId, query);
        
        return Ok(persons.Select(p => new PersonSummaryDto(
            p.Id,
            p.FirstName,
            p.LastName,
            p.FullName,
            p.Age,
            p.Removed
        )));
    }

    [HttpGet("troop/{troopId:int}")]
    public async Task<ActionResult<IEnumerable<PersonSummaryDto>>> GetByTroop(int troopId)
    {
        var persons = await _personRepository.GetByTroopAsync(troopId);
        
        return Ok(persons.Select(p => new PersonSummaryDto(
            p.Id,
            p.FirstName,
            p.LastName,
            p.FullName,
            p.Age,
            p.Removed
        )));
    }

    private static PersonDetailDto MapToDetailDto(Core.Entities.Person p) => new(
        p.Id,
        p.FirstName,
        p.LastName,
        p.BirthDate,
        p.PersonalNumber?.ToString(),
        p.Email,
        p.Phone,
        p.Mobile,
        p.AltEmail,
        p.MumName,
        p.MumEmail,
        p.MumMobile,
        p.DadName,
        p.DadEmail,
        p.DadMobile,
        p.Street,
        p.ZipCode,
        p.ZipName,
        p.MemberYears,
        p.Removed
    );
}
