using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Shared.DTOs;

namespace Skojjt.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SemestersController : ControllerBase
{
    private readonly ISemesterRepository _semesterRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SemestersController(ISemesterRepository semesterRepository, IUnitOfWork unitOfWork)
    {
        _semesterRepository = semesterRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SemesterDto>>> GetAll()
    {
        var semesters = await _semesterRepository.GetAllOrderedAsync();
        return Ok(semesters.Select(s => new SemesterDto(s.Id, s.Year, s.IsAutumn, s.DisplayName)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SemesterDto>> GetById(string id)
    {
        var semester = await _semesterRepository.GetByIdAsync(id);
        if (semester == null)
            return NotFound();

        return Ok(new SemesterDto(semester.Id, semester.Year, semester.IsAutumn, semester.DisplayName));
    }

    [HttpGet("current")]
    public async Task<ActionResult<SemesterDto>> GetCurrent()
    {
        var semester = await _semesterRepository.GetCurrentSemesterAsync();
        if (semester == null)
            return NotFound();

        return Ok(new SemesterDto(semester.Id, semester.Year, semester.IsAutumn, semester.DisplayName));
    }

    [HttpPost]
    public async Task<ActionResult<SemesterDto>> Create([FromBody] CreateSemesterRequest request)
    {
        var id = Semester.GenerateId(request.Year, request.IsAutumn);
        
        if (await _semesterRepository.ExistsAsync(id))
            return Conflict("Semester already exists");

        var semester = new Semester(id, request.Year, request.IsAutumn);

        await _semesterRepository.AddAsync(semester);
        await _unitOfWork.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = semester.Id }, 
            new SemesterDto(semester.Id, semester.Year, semester.IsAutumn, semester.DisplayName));
    }
}

public record CreateSemesterRequest(int Year, bool IsAutumn);
