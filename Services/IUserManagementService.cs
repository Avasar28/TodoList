using TodoListApp.Models;
using TodoListApp.ViewModels;

namespace TodoListApp.Services
{
    public interface IUserManagementService
    {
        Task<(bool Success, string Message, string? RedirectUrl)> CreateUserAsync(AdminCreateUserViewModel model, string? grantedBy);
    }
}
