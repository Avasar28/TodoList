using Microsoft.AspNetCore.Identity;
using TodoListApp.Models;
using System.Text.Json;
using System.IO;

namespace TodoListApp.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            
            string[] roleNames = { "SuperAdmin", "Admin", "Manager", "PrivateUser", "NormalUser" };
            
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Legacy Import
            await ImportLegacyUsers(userManager);

            // Bootstrap SuperAdmin
            string bootstrapEmail = "savaliyaavasar@gmail.com";
            var user = await userManager.FindByEmailAsync(bootstrapEmail);
            if (user != null)
            {
                if (!await userManager.IsInRoleAsync(user, "SuperAdmin"))
                {
                    await userManager.AddToRoleAsync(user, "SuperAdmin");
                    System.Diagnostics.Debug.WriteLine($"[BOOTSTRAP] Promoted {bootstrapEmail} to SuperAdmin.");
                }
            }
        }

        private static async Task ImportLegacyUsers(UserManager<ApplicationUser> userManager)
        {
            var legacyFiles = new[] { "user.json", "users.json" };
            foreach (var file in legacyFiles)
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), file);
                if (!File.Exists(path)) continue;

                try
                {
                    var json = await File.ReadAllTextAsync(path);
                    using var doc = JsonDocument.Parse(json);
                    foreach (var u in doc.RootElement.EnumerateArray())
                    {
                        var email = u.GetProperty("Email").GetString();
                        if (string.IsNullOrEmpty(email)) continue;

                        if (await userManager.FindByEmailAsync(email) == null)
                        {
                            var name = u.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : email.Split('@')[0];
                            var passwordHash = u.GetProperty("Password").GetString();
                            var isAdmin = u.TryGetProperty("IsAdmin", out var adminProp) && adminProp.GetBoolean();

                            var newUser = new ApplicationUser
                            {
                                UserName = email,
                                Email = email,
                                Name = name,
                                EmailConfirmed = true,
                                PasswordHash = passwordHash // Will be verified by LegacyPasswordHasher
                            };

                            var result = await userManager.CreateAsync(newUser);
                            if (result.Succeeded)
                            {
                                var targetRole = isAdmin ? "Admin" : "NormalUser";
                                // Special case for bootstrap email
                                if (email.Equals("savaliyaavasar@gmail.com", StringComparison.OrdinalIgnoreCase)) targetRole = "SuperAdmin";
                                
                                await userManager.AddToRoleAsync(newUser, targetRole);
                                System.Diagnostics.Debug.WriteLine($"[IMPORT] Imported legacy user: {email} as {targetRole}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IMPORT ERROR] Failed to import from {file}: {ex.Message}");
                }
            }
        }
    }
}
