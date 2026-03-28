namespace InticooInspection.Domain.Entities
{
    public class Inspection
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public InspectionStatus Status { get; set; } = InspectionStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string CreatedById { get; set; } = "";

        // Navigation
        public AppUser? CreatedBy { get; set; }
        public ICollection<InspectionStep> Steps { get; set; } = new List<InspectionStep>();
    }

    public class InspectionStep
    {
        public int Id { get; set; }
        public int InspectionId { get; set; }
        public int Order { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public StepStatus Status { get; set; } = StepStatus.Pending;
        public string? Note { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Navigation
        public Inspection? Inspection { get; set; }
    }

    public enum InspectionStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum StepStatus
    {
        Pending = 0,
        Pass = 1,
        Fail = 2,
        Skipped = 3
    }
}
