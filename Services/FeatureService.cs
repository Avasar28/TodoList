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
            // SuperAdmin and Admin get ALL features automatically
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                {
                    return await _context.SystemFeatures.ToListAsync();
                }

                // Enforce strict Manager permissions (Retroactive Override)
                // This ensures existing managers also lose access to everything else immediately
                if (roles.Contains("Manager"))
                {
                    return await _context.SystemFeatures
                        .Where(f => f.TechnicalName == "Page_UserManagement")
                        .ToListAsync();
                }
            }

            // For other users, return their granted features
            return await _context.UserFeatures
                .Where(uf => uf.UserId == userId)
                .Include(uf => uf.Feature)
                .Select(uf => uf.Feature)
                .ToListAsync();
        }

        public async Task<(bool Success, string Message)> UpdateUserFeaturesAsync(string userId, List<int> featureIds, string updatedBy)
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
                var existing = _context.UserFeatures.Where(uf => uf.UserId == userId);
                _context.UserFeatures.RemoveRange(existing);

                foreach (var fId in featureIds)
                {
                    _context.UserFeatures.Add(new UserFeatureAccess
                    {
                        UserId = userId,
                        FeatureId = fId,
                        GrantedBy = updatedBy,
                        GrantedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();

                // 4. Log Activity
                await LogActivityAsync("Update Features", userId, updatedBy, $"Granted {featureIds.Count} features.");

                return (true, "Feature access updated successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating features: {ex.Message}");
            }
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
