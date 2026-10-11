using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAIBot.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminController : Controller
{
    [HttpGet("/Admin/Employees")]
    public IActionResult Employees() => View();
}
