using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Models;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnectMvc.Controllers;

[ApiController]
[Route("api/servicerequests")]
public class ServiceRequestsApiController : ControllerBase
{
    private readonly IRequestService _requestService;

    public ServiceRequestsApiController(IRequestService requestService)
        => _requestService = requestService;

    [HttpGet]
    public ActionResult<List<ServiceRequest>> GetAll() => Ok(_requestService.GetAll());

    [HttpGet("{id:int}")]
    public ActionResult<ServiceRequest> GetById(int id)
    {
        var request = _requestService.GetById(id);
        return request == null ? NotFound() : Ok(request);
    }

    [HttpPost]
    public ActionResult<ServiceRequest> Create(ServiceRequestViewModel model)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var request = _requestService.Create(model);
            return CreatedAtAction(nameof(GetById), new { id = request.RequestId }, request);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/assign/{staffId:int}")]
    public IActionResult Assign(int id, int staffId)
    {
        try
        {
            _requestService.Assign(id, staffId);
            return NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/advance")]
    public IActionResult Advance(int id)
    {
        try
        {
            _requestService.MoveToNextStatus(id);
            return NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
