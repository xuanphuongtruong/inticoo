namespace InticooInspection.Client.Services
{
    public class UserDto
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
