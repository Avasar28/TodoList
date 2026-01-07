using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class JsonUserService : IUserService
    {
        private readonly string _filePath;

        public JsonUserService()
        {
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "user.json");
            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, "[]");
            }
        }

        private List<User> ReadUsers()
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            catch
            {
                return new List<User>();
            }
        }

        private void WriteUsers(List<User> users)
        {
            var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        public User? ValidateUser(string email, string password)
        {
            var users = ReadUsers();
            var user = users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            
            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                if (!user.IsEmailVerified) return null; // Optional: can be handled in controller for better UX
                return user;
            }
            return null;
        }

        public bool RegisterUser(string email, string password, string fullName, bool isVerified = false)
        {
            var users = ReadUsers();
            if (UserExists(email))
            {
                return false; // Already exists
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            
            // Auto-increment ID starting from 1
            int newId = (users.Max(u => (int?)u.Id) ?? 0) + 1;
            
            var newUser = new User 
            { 
                Id = newId,
                Email = email, 
                Password = passwordHash, 
                Name = fullName,
                IsEmailVerified = isVerified 
            };
            users.Add(newUser);
            WriteUsers(users);
            return true;
        }

        public IEnumerable<User> GetAllUsers()
        {
            return ReadUsers();
        }

        public bool UserExists(string email)
        {
            var users = ReadUsers();
            return users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }

        public string? GenerateResetToken(string email)
        {
            var users = ReadUsers();
            var user = users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            
            if (user != null && user.IsEmailVerified)
            {
                user.ResetToken = Guid.NewGuid().ToString();
                user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
                WriteUsers(users);
                Console.WriteLine($"[DEBUG] Generated Reset Token for {email}: {user.ResetToken}");
                return user.ResetToken;
            }
            Console.WriteLine($"[DEBUG] Failed to generate reset token for {email}. User found? {user != null}, Verified? {user?.IsEmailVerified}");
            return null;
        }

        public bool ResetPasswordWithToken(string email, string token, string newPassword)
        {
            var users = ReadUsers();
            var user = users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            
            if (user != null && 
                user.ResetToken == token && 
                user.ResetTokenExpiry > DateTime.UtcNow)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.ResetToken = null;
                user.ResetTokenExpiry = null;
                WriteUsers(users);
                Console.WriteLine($"[DEBUG] Successfully reset password for {email}");
                return true;
            }
            Console.WriteLine($"[DEBUG] Reset password failed for {email}. Token match? {user?.ResetToken == token}, Expired? {user?.ResetTokenExpiry <= DateTime.UtcNow}");
            return false;
        }

        public User? GetUser(int userId)
        {
            var users = ReadUsers();
            return users.FirstOrDefault(u => u.Id == userId);
        }

        public bool UpdatePreferences(int userId, UserPreferences preferences)
        {
            var users = ReadUsers();
            var user = users.FirstOrDefault(u => u.Id == userId);
            
            if (user != null)
            {
                user.Preferences = preferences;
                WriteUsers(users);
                return true;
            }
            return false;
        }

        public bool UpdateUser(User user)
        {
            var users = ReadUsers();
            var existing = users.FirstOrDefault(u => u.Id == user.Id);
            
            if (existing != null)
            {
                existing.Name = user.Name;
                existing.Email = user.Email;
                // Only update password if strictly necessary, but for now let's keep it simple
                // existing.Password = user.Password; 
                WriteUsers(users);
                return true;
            }
            return false;
        }

        public bool DeleteUser(int userId)
        {
            var users = ReadUsers();
            var user = users.FirstOrDefault(u => u.Id == userId);
            
            if (user != null)
            {
                users.Remove(user);
                WriteUsers(users);
                return true;
            }
            return false;
        }
    }
}
