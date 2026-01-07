using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface IUserService
    {
        User? ValidateUser(string email, string password);
        bool RegisterUser(string email, string password, string fullName, bool isVerified = false);
        bool UserExists(string email);
        string? GenerateResetToken(string email);
        bool ResetPasswordWithToken(string email, string token, string newPassword);
        
        // Personalization
        User? GetUser(int userId);
        IEnumerable<User> GetAllUsers();
        bool UpdatePreferences(int userId, UserPreferences preferences);
        bool UpdateUser(User user);
        bool DeleteUser(int userId);
    }
}
