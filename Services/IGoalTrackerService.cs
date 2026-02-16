using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface IGoalTrackerService
    {
        Task<IEnumerable<Goal>> GetUserGoalsAsync(string userId);
        Task<Goal> CreateGoalAsync(Goal goal, string userId);
        Task<(bool Success, string Message)> UpdateGoalAsync(Goal goal, string userId);
        Task<(bool Success, string Message)> UpdateProgressAsync(int goalId, decimal currentValue, string userId);
        Task<(bool Success, string Message)> DeleteGoalAsync(int goalId, string userId);
    }
}
