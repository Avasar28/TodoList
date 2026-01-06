using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface IUserService
    {
        User? ValidateUser(string email, string password);
        bool RegisterUser(string email, string password, bool isVerified = false);
        bool UserExists(string email);
        string? GenerateResetToken(string email);
        bool ResetPasswordWithToken(string email, string token, string newPassword);
    }
}
