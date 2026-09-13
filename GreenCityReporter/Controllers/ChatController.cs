using GreenCityReporter.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GreenCityReporter.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private const int MaxMessageLength = 500;
        private const string UnavailableMessage =
            "The AI assistant is temporarily unavailable. You can still use the normal Green City Reporter features.";
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService chatService,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ask(
            [FromForm] string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogInformation("Chat request received. Message length: {Length}", message?.Length ?? 0);
                return BadRequest(new { error = "Please enter a message." });
            }

            _logger.LogInformation("Chat request received. Message length: {Length}", message.Length);

            if (message.Length > MaxMessageLength)
            {
                return BadRequest(new { error = "Messages cannot exceed 500 characters." });
            }

            var response = await _chatService.AskAsync(
                message,
                User,
                HttpContext.RequestAborted);

            return Json(new
            {
                message = response ?? UnavailableMessage
            });
        }
    }
}
