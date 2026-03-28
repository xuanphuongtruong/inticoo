namespace InticooInspection.Application.DTOs
{
    // ── Auth ────────────────────────────────────────────────────────────────────

    public class LoginDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }

    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Message { get; set; }
        public UserInfoDto? User { get; set; }
    }

    public class UserInfoDto
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "User";
    }

    // ── Inspection ───────────────────────────────────────────────────────────────

    public class InspectionDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CreatedByName { get; set; } = "";
        public int TotalSteps { get; set; }
        public int CompletedSteps { get; set; }
        public List<InspectionStepDto> Steps { get; set; } = new();
    }

    public class InspectionStepDto
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Note { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class CreateInspectionDto
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public List<CreateStepDto> Steps { get; set; } = new();
    }

    public class CreateStepDto
    {
        public int Order { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
    }

    public class UpdateStepDto
    {
        public int StepId { get; set; }
        public string Status { get; set; } = "";
        public string? Note { get; set; }
    }

    // ── Dashboard ────────────────────────────────────────────────────────────────

    public class DashboardDto
    {
        public int TotalInspections { get; set; }
        public int PendingInspections { get; set; }
        public int InProgressInspections { get; set; }
        public int CompletedInspections { get; set; }
        public double CompletionRate { get; set; }
        public List<InspectionDto> RecentInspections { get; set; } = new();
    }
}
