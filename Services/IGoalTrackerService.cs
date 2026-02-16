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

        // Schedule Methods
        Task<IEnumerable<GoalSchedule>> GetUserSchedulesAsync(string userId);
        Task<GoalSchedule> CreateScheduleAsync(GoalSchedule schedule, string userId);
        Task<(bool Success, string Message)> DeleteScheduleAsync(int scheduleId, string userId);
        Task<GoalAnalyticsDto> GetGoalAnalyticsAsync(string userId);
        Task<IEnumerable<UserAchievement>> GetUserAchievementsAsync(string userId);
        
        // Sub-task Methods
        Task<IEnumerable<GoalSubTask>> GetSubTasksAsync(int goalId, string userId);
        Task<GoalSubTask> AddSubTaskAsync(int goalId, string title, string userId);
        Task<bool> ToggleSubTaskAsync(int subTaskId, string userId);
        Task<bool> DeleteSubTaskAsync(int subTaskId, string userId);
        Task<IEnumerable<string>> GetSuggestedSubTasksAsync(int goalId, string userId);
    }
}
