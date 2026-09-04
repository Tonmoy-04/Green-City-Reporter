using GreenCityReporter.Models;

namespace GreenCityReporter.ViewModels
{
    public class CitizenDashboardViewModel
    {
        public int TotalReports { get; set; }
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int ResolvedCount { get; set; }
        public int RejectedCount { get; set; }

        public double ResolutionRate => TotalReports > 0 ? (double)ResolvedCount / TotalReports * 100 : 0;

        public List<Report> RecentReports { get; set; } = new List<Report>();
        public List<Notification> RecentNotifications { get; set; } = new List<Notification>();
        public int UnreadNotificationCount { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    }

    public class UserProfileViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Citizen";
        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;

        public int TotalReportsCount { get; set; }
        public int ResolvedReportsCount { get; set; }

        // Optional Password Change
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmNewPassword { get; set; }
    }
}
