using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TodoListApp.Data;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class FeatureService : IFeatureService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FeatureService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IEnumerable<SystemFeature>> GetAllFeaturesAsync()
        {
            return await _context.SystemFeatures.OrderBy(f => f.Type).ThenBy(f => f.Name).ToListAsync();
        }

        public async Task<IEnumerable<SystemFeature>> GetUserGrantedFeaturesAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Enumerable.Empty<SystemFeature>();

            var roles = await _userManager.GetRolesAsync(user);

            // SuperAdmin always gets everything
            if (roles.Contains("SuperAdmin"))
            {
                return await _context.SystemFeatures.ToListAsync();
            }

            // Get specific grants from database
            var granted = await _context.UserFeatures
                .Where(uf => uf.UserId == userId)
                .Include(uf => uf.Feature)
                .Select(uf => uf.Feature)
                .ToListAsync();

            // If custom grants exist, return them (this is the new system)
            if (granted.Any())
            {
                return granted;
            }

            // Fallback for users who haven't been customized yet (Legacy behavior)
            if (roles.Contains("Admin"))
            {
                // Admin gets everything EXCEPT User Management by default
                return await _context.SystemFeatures
                    .Where(f => f.TechnicalName != "Page_UserManagement")
                    .ToListAsync();
            }
            if (roles.Contains("Manager"))
            {
                return await _context.SystemFeatures
                    .Where(f => f.TechnicalName == "Page_UserManagement")
                    .ToListAsync();
            }

            return granted;
        }

        public async Task<(bool Success, string Message)> UpdateUserFeaturesAsync(string userId, List<int> featureIds, string updatedBy, bool applyToRole = false)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return (false, "User not found.");

                // 1. Validate Feature IDs server-side
                var validFeatureIds = await _context.SystemFeatures.Select(f => f.Id).ToListAsync();
                if (featureIds.Any(id => !validFeatureIds.Contains(id)))
                {
                    return (false, "Invalid feature IDs detected. Manual tampering prevented.");
                }

                // 2. Self-Preservation Logic: Prevent Admin from removing their own User Management access
                var currentUser = await _userManager.FindByNameAsync(updatedBy);
                if (currentUser != null && currentUser.Id == userId)
                {
                    var userManagementFeature = await _context.SystemFeatures
                        .FirstOrDefaultAsync(f => f.TechnicalName == "Page_UserManagement");
                    
                    if (userManagementFeature != null && !featureIds.Contains(userManagementFeature.Id))
                    {
                        // Check if they currently HAVE it. If so, they shouldn't be able to remove it.
                        var hasAccess = await _context.UserFeatures.AnyAsync(uf => uf.UserId == userId && uf.FeatureId == userManagementFeature.Id);
                        if (hasAccess)
                        {
                            return (false, "Self-Preservation: You cannot revoke your own 'User Management' access to prevent accidental lockout.");
                        }
                    }
                }

                // 3. Perform Update
                var userRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
                var userIdsToUpdate = new List<string> { userId };

                if (applyToRole && !string.IsNullOrEmpty(userRole))
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(userRole);
                    userIdsToUpdate = usersInRole.Select(u => u.Id).Distinct().ToList();
                }

                // Remove existing features for all targeted users
                var existing = _context.UserFeatures.Where(uf => userIdsToUpdate.Contains(uf.UserId));
                _context.UserFeatures.RemoveRange(existing);

                foreach (var targetUserId in userIdsToUpdate)
                {
                    foreach (var fId in featureIds)
                    {
                        _context.UserFeatures.Add(new UserFeatureAccess
                        {
                            UserId = targetUserId,
                            FeatureId = fId,
                            GrantedBy = updatedBy,
                            GrantedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // 4. Log Activity
                var logMsg = applyToRole ? $"Updated features for all users in role '{userRole}'." : $"Granted {featureIds.Count} features.";
                await LogActivityAsync("Update Features", userId, updatedBy, logMsg);

                return (true, "Feature access updated successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating features: {ex.Message}");
            }
        }

        public async Task<List<int>> GetDefaultFeatureIdsForRoleAsync(string roleName)
        {
            var allFeatures = await _context.SystemFeatures.ToListAsync();
            
            return roleName switch
            {
                "SuperAdmin" => allFeatures.Select(f => f.Id).ToList(),
                
                "Admin" => allFeatures
                    .Where(f => f.TechnicalName != "Page_UserManagement")
                    .Select(f => f.Id).ToList(),
                
                "Manager" => allFeatures
                    .Where(f => f.TechnicalName == "Page_UserManagement")
                    .Select(f => f.Id).ToList(),
                
                "PrivateUser" => allFeatures
                    .Where(f => new[] { "Page_Tasks", "Page_TimeTracker", "Page_Dashboard", "Widget_Weather", "Widget_Currency", "Widget_Time", "Widget_Habit", "Widget_PdfTools", "Widget_GoalTracker" }.Contains(f.TechnicalName))
                    .Select(f => f.Id).ToList(),
                
                "NormalUser" => allFeatures
                    .Where(f => new[] { "Page_Dashboard", "Page_Tasks", "Page_TimeTracker", "Widget_Weather", "Widget_Currency", "Widget_Time", "Widget_Habit", "Widget_PdfTools", "Widget_News", "Widget_Country", "Widget_Translator", "Widget_Emergency", "Widget_Holiday" }.Contains(f.TechnicalName))
                    .Select(f => f.Id).ToList(),
                
                _ => allFeatures.Where(f => f.IsDefault).Select(f => f.Id).ToList()
            };
        }

        public async Task LogActivityAsync(string action, string userId, string executedBy, string details)
        {
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = action,
                UserId = userId,
                PerformedBy = executedBy,
                Timestamp = DateTime.UtcNow,
                Details = details
            });
            await _context.SaveChangesAsync();
        }
    }
}
