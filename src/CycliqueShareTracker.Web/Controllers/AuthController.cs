using System.Security.Claims;
using CycliqueShareTracker.Web.Configuration;
using CycliqueShareTracker.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Web.Controllers;

public class AuthController : Controller
{
    private readonly AuthOptions _authOptions;

    public AuthController(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }

    [HttpGet("auth/login")]
    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    [HttpPost("auth/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Username != _authOptions.Username || model.Password != _authOptions.Password)
        {
            ModelState.AddModelError(string.Empty, "Identifiants invalides.");
            return View(model);
        }

        var claims = new[] { new Claim(ClaimTypes.Name, model.Username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("auth/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
