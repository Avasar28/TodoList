using System.Collections.Generic;
using System.Threading.Tasks;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface IFeatureService
    {
        Task<IEnumerable<SystemFeature>> GetAllFeaturesAsync();
        Task<IEnumerable<SystemFeature>> GetUserGrantedFeaturesAsync(string userId);
        Task<(bool Success, string Message)> UpdateUserFeaturesAsync(string userId, List<int> featureIds, string updatedBy);
        Task LogActivityAsync(string action, string userId, string executedBy, string details);
    }
}
