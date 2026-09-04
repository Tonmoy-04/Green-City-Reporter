using GreenCityReporter.Models;
using GreenCityReporter.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GreenCityReporter.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly GreenCityReporter.Data.ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            GreenCityReporter.Data.ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser 
                { 
                    UserName = model.Email, 
                    Email = model.Email, 
                    FullName = model.FullName 
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // By default, a sign-up makes the user a Citizen
                    await _userManager.AddToRoleAsync(user, "Citizen");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
{
    ViewData["ReturnUrl"] = returnUrl;

    if (ModelState.IsValid)
    {
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false
        );

        if (result.Succeeded)
        {
            // Find the logged-in user
            var loggedInUser = await _userManager.FindByEmailAsync(model.Email);

            // Admin -> Admin Dashboard
            if (loggedInUser != null &&
                await _userManager.IsInRoleAsync(loggedInUser, "Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }

            // Citizen -> requested page / Home
            return RedirectToLocal(returnUrl);
        }

        ModelState.AddModelError(
            string.Empty,
            "Invalid login attempt."
        );
    }

    return View(model);
}

        // GET: /Account/Profile
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            string role = roles.FirstOrDefault() ?? "Citizen";

            int totalReports = _context.Reports.Count(r => r.UserId == user.Id);
            int resolvedReports = _context.Reports.Count(r => r.UserId == user.Id && r.CurrentStatus == Models.Enums.ReportStatus.Resolved);

            var viewModel = new ViewModels.UserProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = role,
                TotalReportsCount = totalReports,
                ResolvedReportsCount = resolvedReports
            };

            return View(viewModel);
        }

        // POST: /Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Profile(ViewModels.UserProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                int totalReports = _context.Reports.Count(r => r.UserId == user.Id);
                int resolvedReports = _context.Reports.Count(r => r.UserId == user.Id && r.CurrentStatus == Models.Enums.ReportStatus.Resolved);

                model.Email = user.Email ?? string.Empty;
                model.TotalReportsCount = totalReports;
                model.ResolvedReportsCount = resolvedReports;
                return View(model);
            }

            // Update Full Name
            if (user.FullName != model.FullName.Trim())
            {
                user.FullName = model.FullName.Trim();
                await _userManager.UpdateAsync(user);
                TempData["ProfileSuccess"] = "Profile name updated successfully.";
            }

            // Handle Password Change if requested
            if (!string.IsNullOrWhiteSpace(model.CurrentPassword) && !string.IsNullOrWhiteSpace(model.NewPassword))
            {
                if (model.NewPassword != model.ConfirmNewPassword)
                {
                    ModelState.AddModelError("ConfirmNewPassword", "New password and confirmation password do not match.");
                    return View(model);
                }

                var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (changePasswordResult.Succeeded)
                {
                    await _signInManager.RefreshSignInAsync(user);
                    TempData["PasswordSuccess"] = "Password changed successfully.";
                }
                else
                {
                    foreach (var error in changePasswordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(model);
                }
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }
    }
}