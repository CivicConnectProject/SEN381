using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Models;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnectMvc.Controllers;

public class ServiceRequestsController : Controller
{
    private readonly IRequestService _requestService;

    public ServiceRequestsController(IRequestService requestService) => _requestService = requestService;

    public IActionResult Index() => View(_requestService.GetAll());

    public IActionResult Details(int id)
    {
        var request = _requestService.GetById(id);
        ViewBag.AssignableStaff = _requestService.GetAssignableStaff();
        return request == null ? NotFound() : View(request);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.Categories = _requestService.GetCategories();
        return View(new ServiceRequestViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ServiceRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = _requestService.GetCategories();
            return View(model);
        }

        try
        {
            var request = _requestService.Create(model);
            return RedirectToAction(nameof(Details), new { id = request.RequestId });
        }
        catch (KeyNotFoundException ex)
        {
            ModelState.AddModelError(nameof(model.CategoryId), ex.Message);
            ViewBag.Categories = _requestService.GetCategories();
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Assign(int id, int staffId)
    {
        try
        {
            _requestService.Assign(id, staffId);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Advance(int id)
    {
        try
        {
            _requestService.MoveToNextStatus(id);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
