using Microsoft.AspNetCore.Identity;
using TodoListApp.Models;
using System.Text.Json;
using System.IO;
using Microsoft.EntityFrameworkCore;

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

            // Seed Features
            await SeedFeaturesAsync(serviceProvider);

            // Legacy Import - Disabled to prevent re-importing deleted users
            // await ImportLegacyUsers(userManager);

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

        private static async Task SeedFeaturesAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            
            var features = new List<SystemFeature>
            {
                // Pages
                new SystemFeature { Name = "Dashboard", TechnicalName = "Page_Dashboard", Type = FeatureType.Page, Icon = "📊", Description = "Access to the main analytics dashboard.", IsDefault = true },
                new SystemFeature { Name = "My Tasks", TechnicalName = "Page_Tasks", Type = FeatureType.Page, Icon = "✅", Description = "Access to personal todo list management.", IsDefault = true },
                new SystemFeature { Name = "Time Tracker", TechnicalName = "Page_TimeTracker", Type = FeatureType.Page, Icon = "⏱️", Description = "Access to the productivity time tracking tool.", IsDefault = true },
                new SystemFeature { Name = "PDF Tools", TechnicalName = "Page_PdfTools", Type = FeatureType.Page, Icon = "📄", Description = "Access to PDF merge and management tools.", IsDefault = true },
                new SystemFeature { Name = "User Management", TechnicalName = "Page_UserManagement", Type = FeatureType.Page, Icon = "👥", Description = "Access to manage system users and roles.", IsDefault = false },

                // Widgets
                new SystemFeature { Name = "Weather Widget", TechnicalName = "Widget_Weather", Type = FeatureType.Widget, Icon = "🌤️", Description = "Real-time weather information on dashboard.", IsDefault = true },
                new SystemFeature { Name = "News Widget", TechnicalName = "Widget_News", Type = FeatureType.Widget, Icon = "📰", Description = "Daily news feed on dashboard.", IsDefault = true },
                new SystemFeature { Name = "Currency Widget", TechnicalName = "Widget_Currency", Type = FeatureType.Widget, Icon = "💱", Description = "Live currency conversion rates.", IsDefault = true },
                new SystemFeature { Name = "Holiday Widget", TechnicalName = "Widget_Holiday", Type = FeatureType.Widget, Icon = "📅", Description = "Public holiday calendar for selected countries.", IsDefault = true },
                new SystemFeature { Name = "Time Widget", TechnicalName = "Widget_Time", Type = FeatureType.Widget, Icon = "🕒", Description = "Digital clock and time conversion widgets.", IsDefault = true },
                new SystemFeature { Name = "Habit Widget", TechnicalName = "Widget_Habit", Type = FeatureType.Widget, Icon = "🌱", Description = "Daily habit tracking and streaks.", IsDefault = true },
                new SystemFeature { Name = "Country Widget", TechnicalName = "Widget_Country", Type = FeatureType.Widget, Icon = "🌍", Description = "Explore global country details and statistics.", IsDefault = true },
                new SystemFeature { Name = "Translator Widget", TechnicalName = "Widget_Translator", Type = FeatureType.Widget, Icon = "🈶", Description = "Quick text translation tool.", IsDefault = true },
                new SystemFeature { Name = "Emergency Widget", TechnicalName = "Widget_Emergency", Type = FeatureType.Widget, Icon = "🆘", Description = "Emergency SOS and global contact numbers.", IsDefault = true },
                new SystemFeature { Name = "PDF Widget", TechnicalName = "Widget_PdfTools", Type = FeatureType.Widget, Icon = "📄", Description = "Quick access to PDF tools from dashboard.", IsDefault = true },
                new SystemFeature { Name = "Goal Tracker", TechnicalName = "Widget_GoalTracker", Type = FeatureType.Widget, Icon = "🎯", Description = "Track and manage your personal goals.", IsDefault = true }
            };

            foreach (var feature in features)
            {
                if (!await context.SystemFeatures.AnyAsync(f => f.TechnicalName == feature.TechnicalName))
                {
                    await context.SystemFeatures.AddAsync(feature);
                }
            }
            
            await context.SaveChangesAsync();
        }

        private static async Task ImportLegacyUsers(UserManager<ApplicationUser> userManager)
        {
            // Check if any users exist (excluding the bootstrap user)
            var existingUsers = userManager.Users.ToList();
            if (existingUsers.Any(u => !u.Email.Equals("savaliyaavasar@gmail.com", StringComparison.OrdinalIgnoreCase)))
            {
                // Users already exist, skip import to prevent re-importing deleted users
                System.Diagnostics.Debug.WriteLine("[IMPORT] Skipping legacy import - users already exist in database.");
                return;
            }

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
