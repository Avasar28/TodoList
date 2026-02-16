using Microsoft.EntityFrameworkCore;
using TodoListApp.Data;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class GoalTrackerService : IGoalTrackerService
    {
        private readonly ApplicationDbContext _context;

        public GoalTrackerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Goal>> GetUserGoalsAsync(string userId)
        {
            return await _context.Goals
                .Where(g => g.UserId == userId)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();
        }

        public async Task<Goal> CreateGoalAsync(Goal goal, string userId)
        {
            goal.UserId = userId;
            goal.CreatedAt = DateTime.UtcNow;
            goal.UpdatedAt = DateTime.UtcNow;
            
            CalculateProgress(goal);
            UpdateStatus(goal);

            // Basic validation to ensure start date isn't after end date
            if (goal.StartDate > goal.EndDate)
            {
                goal.EndDate = goal.StartDate.AddMonths(1);
            }

            _context.Goals.Add(goal);
            await _context.SaveChangesAsync();
            return goal;
        }

        public async Task<(bool Success, string Message)> UpdateGoalAsync(Goal goal, string userId)
        {
            var existing = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goal.Id && g.UserId == userId);
            if (existing == null) return (false, "Goal not found.");

            existing.Title = goal.Title;
            existing.Description = goal.Description;
            existing.Category = goal.Category;
            existing.Priority = goal.Priority;
            existing.TargetValue = goal.TargetValue;
            existing.Unit = goal.Unit;
            existing.StartDate = goal.StartDate;
            existing.EndDate = goal.EndDate;
            existing.UpdatedAt = DateTime.UtcNow;

            CalculateProgress(existing);
            UpdateStatus(existing);

            _context.Update(existing);
            await _context.SaveChangesAsync();
            return (true, "Goal updated successfully.");
        }

        public async Task<(bool Success, string Message)> UpdateProgressAsync(int goalId, decimal currentValue, string userId)
        {
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
            if (goal == null) return (false, "Goal not found.");

            if (currentValue > goal.TargetValue) currentValue = goal.TargetValue;
            if (currentValue < 0) currentValue = 0;

            goal.CurrentValue = currentValue;
            goal.UpdatedAt = DateTime.UtcNow;

            CalculateProgress(goal);
            UpdateStatus(goal);

            _context.Update(goal);
            await _context.SaveChangesAsync();
            return (true, "Progress updated successfully.");
        }

        public async Task<(bool Success, string Message)> DeleteGoalAsync(int goalId, string userId)
        {
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
            if (goal == null) return (false, "Goal not found.");

            _context.Goals.Remove(goal);
            await _context.SaveChangesAsync();
            return (true, "Goal deleted successfully.");
        }

        public async Task<IEnumerable<GoalSchedule>> GetUserSchedulesAsync(string userId)
        {
            return await _context.GoalSchedules
                .Include(s => s.Goal)
                .Where(s => s.Goal!.UserId == userId)
                .OrderBy(s => s.ScheduledDate)
                .ThenBy(s => s.StartTime)
                .ToListAsync();
        }

        public async Task<GoalSchedule> CreateScheduleAsync(GoalSchedule schedule, string userId)
        {
            // Verify goal ownership
            var goal = await _context.Goals.AnyAsync(g => g.Id == schedule.GoalId && g.UserId == userId);
            if (!goal) throw new UnauthorizedAccessException("You do not own this goal.");

            schedule.CreatedAt = DateTime.UtcNow;
            _context.GoalSchedules.Add(schedule);
            await _context.SaveChangesAsync();
            return schedule;
        }

        public async Task<(bool Success, string Message)> DeleteScheduleAsync(int scheduleId, string userId)
        {
            var schedule = await _context.GoalSchedules
                .Include(s => s.Goal)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Goal!.UserId == userId);

            if (schedule == null) return (false, "Schedule not found.");

            _context.GoalSchedules.Remove(schedule);
            await _context.SaveChangesAsync();
            return (true, "Schedule removed.");
        }

        private void CalculateProgress(Goal goal)
        {
            if (goal.TargetValue > 0)
            {
                goal.ProgressPercent = (goal.CurrentValue / goal.TargetValue) * 100;
            }
            else
            {
                goal.ProgressPercent = 0;
            }
        }

        private void UpdateStatus(Goal goal)
        {
            if (goal.ProgressPercent >= 100)
            {
                goal.Status = "Completed";
            }
            else if (goal.EndDate < DateTime.UtcNow)
            {
                goal.Status = "Off Track";
            }
            else
            {
                goal.Status = "On Track";
            }
        }
    }
}
