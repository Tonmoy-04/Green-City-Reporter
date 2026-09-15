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
        private readonly IWebHostEnvironment _environment;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            GreenCityReporter.Data.ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _environment = environment;
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
                    return RedirectToAction("Dashboard", "Report");
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
                ProfilePicturePath = user.ProfilePicturePath,
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
                model.ProfilePicturePath = user.ProfilePicturePath;
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

            if (model.ProfilePicture is { Length: > 0 })
            {
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!allowedTypes.Contains(model.ProfilePicture.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("ProfilePicture", "Please upload a JPG, PNG, or WebP image.");
                    return await ProfileWithModel(model, user);
                }

                if (model.ProfilePicture.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ProfilePicture", "Profile pictures must be 5 MB or smaller.");
                    return await ProfileWithModel(model, user);
                }

                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploadsPath);
                var extension = model.ProfilePicture.ContentType.ToLowerInvariant() switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => ".jpg"
                };
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);
                await using (var stream = System.IO.File.Create(filePath))
                {
                    await model.ProfilePicture.CopyToAsync(stream);
                }

                var oldPath = user.ProfilePicturePath;
                user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
                await _userManager.UpdateAsync(user);
                if (!string.IsNullOrWhiteSpace(oldPath))
                {
                    var oldFile = Path.Combine(_environment.WebRootPath, oldPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
                }
                TempData["ProfileSuccess"] = "Profile picture updated successfully.";
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

        private async Task<IActionResult> ProfileWithModel(UserProfileViewModel model, ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            model.Email = user.Email ?? string.Empty;
            model.Role = roles.FirstOrDefault() ?? "Citizen";
            model.ProfilePicturePath = user.ProfilePicturePath;
            model.TotalReportsCount = _context.Reports.Count(r => r.UserId == user.Id);
            model.ResolvedReportsCount = _context.Reports.Count(r => r.UserId == user.Id && r.CurrentStatus == Models.Enums.ReportStatus.Resolved);
            return View("Profile", model);
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
            if (!string.IsNullOrEmpty(returnUrl) &&
                Url.IsLocalUrl(returnUrl) &&
                !string.Equals(returnUrl, "/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(returnUrl, "/Home", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(returnUrl, "/Home/Index", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Dashboard", "Report");
            }
        }
    }
}
