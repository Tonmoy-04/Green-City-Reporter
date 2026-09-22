using System.Text;
using GreenCityReporter.Models;
using GreenCityReporter.Services.Email;
using GreenCityReporter.Services.Storage;
using GreenCityReporter.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GreenCityReporter.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly GreenCityReporter.Data.ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;
        private readonly IAccountEmailSender _emailSender;
        private readonly EmailOptions _emailOptions;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            GreenCityReporter.Data.ApplicationDbContext context,
            IFileStorageService fileStorage,
            IAccountEmailSender emailSender,
            IOptions<EmailOptions> emailOptions,
            IWebHostEnvironment environment,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _fileStorage = fileStorage;
            _emailSender = emailSender;
            _emailOptions = emailOptions.Value;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account-email")]
        public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
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
                    var roleResult = await _userManager.AddToRoleAsync(user, "Citizen");
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError("Could not assign the Citizen role to newly registered user {UserId}.", user.Id);
                    }

                    try
                    {
                        await SendVerificationEmailAsync(user, cancellationToken);
                        TempData["VerificationNotice"] = "We sent a verification email to the address you registered. Open it to activate your account.";
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "Verification email delivery failed for user {UserId}.", user.Id);
                        TempData["VerificationError"] = "Your account was created, but we could not send the verification email. Please use Resend Verification Email or try again later.";
                    }

                    return RedirectToAction(nameof(RegistrationPending));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult RegistrationPending()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                return View(model: false);
            }

            return await ConfirmEmailCoreAsync(userId, code);
        }

        private async Task<IActionResult> ConfirmEmailCoreAsync(string userId, string code)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View("ConfirmEmail", false);
            }

            string token;
            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return View("ConfirmEmail", false);
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Email confirmation failed for user {UserId}.", user.Id);
            }

            return View("ConfirmEmail", result.Succeeded);
        }

        [HttpGet]
        public IActionResult ResendVerificationEmail()
        {
            return View(new ResendVerificationEmailViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account-email")]
        public async Task<IActionResult> ResendVerificationEmail(
            ResendVerificationEmailViewModel model,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
            {
                try
                {
                    await SendVerificationEmailAsync(user, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Resent verification email delivery failed for user {UserId}.", user.Id);
                    ModelState.AddModelError(string.Empty, "We could not send the verification email right now. Please try again later.");
                    return View(model);
                }
            }

            // Use the same response for unknown and already-confirmed addresses to avoid account enumeration.
            ViewData["Submitted"] = true;
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

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "You must verify your email address before signing in.");
            return View(model);
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

                var oldPath = user.ProfilePicturePath;
                var newPath = await _fileStorage.SaveProfilePictureAsync(
                    user.Id,
                    model.ProfilePicture,
                    HttpContext.RequestAborted);
                if (newPath == null)
                {
                    ModelState.AddModelError("ProfilePicture", "The profile picture could not be uploaded. Please try again.");
                    return await ProfileWithModel(model, user);
                }

                user.ProfilePicturePath = newPath;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    user.ProfilePicturePath = oldPath;
                    return await ProfileWithModel(model, user);
                }

                if (!string.IsNullOrWhiteSpace(oldPath) && !string.Equals(oldPath, newPath, StringComparison.Ordinal))
                {
                    await _fileStorage.DeleteProfilePictureAsync(oldPath, HttpContext.RequestAborted);
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

        private async Task SendVerificationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var relativeUrl = Url.Action(
                nameof(ConfirmEmail),
                "Account",
                new { userId = user.Id, code = encodedToken })
                ?? throw new InvalidOperationException("Could not create the email confirmation URL.");

            string verificationUrl;
            if (!string.IsNullOrWhiteSpace(_emailOptions.PublicBaseUrl))
            {
                var publicBaseUri = new Uri(_emailOptions.PublicBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
                verificationUrl = new Uri(publicBaseUri, relativeUrl.TrimStart('/')).ToString();
            }
            else
            {
                if (_environment.IsProduction())
                {
                    throw new InvalidOperationException("A public HTTPS base URL is required in Production.");
                }

                verificationUrl = Url.Action(
                    nameof(ConfirmEmail),
                    "Account",
                    new { userId = user.Id, code = encodedToken },
                    Request.Scheme,
                    Request.Host.ToUriComponent())
                    ?? throw new InvalidOperationException("Could not create an absolute email confirmation URL.");
            }

            await _emailSender.SendVerificationEmailAsync(
                user.Email ?? throw new InvalidOperationException("The registered user has no email address."),
                user.FullName,
                verificationUrl,
                cancellationToken);
        }
    }
}
