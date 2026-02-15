using TodoListApp.Models;

namespace TodoListApp.ViewModels
{
    public class UserListViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string Role { get; set; } = "NormalUser";
    }
}
