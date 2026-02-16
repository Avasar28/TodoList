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

        public async Task<IEnumerable<UserAchievement>> GetUserAchievementsAsync(string userId)
        {
            return await _context.UserAchievements
                .Where(ua => ua.UserId == userId)
                .OrderByDescending(ua => ua.AchievedAt)
                .ToListAsync();
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
            
            await CheckAndAwardAchievementsAsync(userId);
            
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

            bool wasCompleted = goal.ProgressPercent >= 100;
            goal.CurrentValue = currentValue;
            goal.UpdatedAt = DateTime.UtcNow;
            goal.LastProgressUpdate = DateTime.UtcNow;

            CalculateProgress(goal);
            UpdateStatus(goal);

            _context.Update(goal);
            await _context.SaveChangesAsync();

            if (!wasCompleted && goal.ProgressPercent >= 100)
            {
                await CheckAndAwardAchievementsAsync(userId);
            }

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

        public async Task<GoalAnalyticsDto> GetGoalAnalyticsAsync(string userId)
        {
            var goals = await _context.Goals
                .Where(g => g.UserId == userId)
                .ToListAsync();

            var today = DateTime.UtcNow;
            var analytics = new GoalAnalyticsDto
            {
                TotalGoals = goals.Count,
                CompletedGoals = goals.Count(g => g.ProgressPercent >= 100),
                ActiveGoals = goals.Count(g => g.ProgressPercent < 100 && g.EndDate >= today),
                OverdueGoals = goals.Count(g => g.ProgressPercent < 100 && g.EndDate < today),
                CompletionRate = goals.Any() ? (double)goals.Count(g => g.ProgressPercent >= 100) / goals.Count * 100 : 0
            };

            // Productivity Intelligence: Risk Detection & Stagnation
            foreach (var goal in goals.Where(g => g.ProgressPercent < 100))
            {
                var remainingDays = (goal.EndDate - today).TotalDays;
                if (remainingDays > 0)
                {
                    var remainingValue = goal.TargetValue - goal.CurrentValue;
                    var requiredPace = (double)remainingValue / remainingDays;
                    var elapsedDays = (today - goal.StartDate).TotalDays;
                    var actualPace = elapsedDays > 0.5 ? (double)goal.CurrentValue / elapsedDays : 0;

                    if (actualPace > 0 && requiredPace > actualPace * 1.8)
                    {
                        analytics.GoalsAtRisk++;
                    }
                    else if (actualPace == 0 && goal.StartDate.AddDays(3) < today)
                    {
                        analytics.GoalsAtRisk++;
                    }
                }
                else if (goal.EndDate < today)
                {
                    analytics.GoalsAtRisk++;
                }

                if (goal.LastProgressUpdate < today.AddDays(-goal.ReminderFrequencyDays))
                {
                    analytics.StagnantGoals++;
                }
            }

            // Category Breakdown
            analytics.CategoryBreakdown = goals
                .GroupBy(g => g.Category)
                .ToDictionary(g => g.Key ?? "Uncategorized", g => g.Count());

            // Smart Insights
            if (analytics.GoalsAtRisk > 0) analytics.SmartInsights.Add($"{analytics.GoalsAtRisk} goals need immediate attention.");
            if (analytics.StagnantGoals > 0) analytics.SmartInsights.Add($"{analytics.StagnantGoals} goals haven't seen progress recently.");
            if (analytics.OverdueGoals > 0) analytics.SmartInsights.Add($"{analytics.OverdueGoals} overdue milestones.");

            // Monthly Completion Data (Last 6 months)
            for (int i = 5; i >= 0; i--)
            {
                var monthDate = DateTime.UtcNow.AddMonths(-i);
                var monthKey = monthDate.ToString("MMM yyyy");
                var completions = goals.Count(g => g.ProgressPercent >= 100 && 
                                                 g.UpdatedAt.Month == monthDate.Month && 
                                                 g.UpdatedAt.Year == monthDate.Year);
                analytics.MonthlyCompletionData.Add(monthKey, completions);
            }

            return analytics;
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

        private async Task CheckAndAwardAchievementsAsync(string userId)
        {
            var goals = await _context.Goals.Where(g => g.UserId == userId).ToListAsync();
            var existingAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.AchievementName)
                .ToListAsync();

            var achievementsToAward = new List<UserAchievement>();

            // 1. Trailblazer: First Goal Created
            if (goals.Count >= 1 && !existingAchievements.Contains("Trailblazer"))
            {
                achievementsToAward.Add(new UserAchievement {
                    UserId = userId,
                    AchievementName = "Trailblazer",
                    Description = "Created your first milestone!",
                    Icon = "🚀",
                    AchievedAt = DateTime.UtcNow
                });
            }

            var completedGoals = goals.Where(g => g.ProgressPercent >= 100).ToList();

            // 2. Finisher: First Goal Completed
            if (completedGoals.Count >= 1 && !existingAchievements.Contains("Finisher"))
            {
                achievementsToAward.Add(new UserAchievement {
                    UserId = userId,
                    AchievementName = "Finisher",
                    Description = "Completed your first milestone!",
                    Icon = "✅",
                    AchievedAt = DateTime.UtcNow
                });
            }

            // 3. High Five: 5 Goals Completed
            if (completedGoals.Count >= 5 && !existingAchievements.Contains("High Five"))
            {
                achievementsToAward.Add(new UserAchievement {
                    UserId = userId,
                    AchievementName = "High Five",
                    Description = "Successfully completed 5 milestones!",
                    Icon = "🖐️",
                    AchievedAt = DateTime.UtcNow
                });
            }

            // 4. Elite: 10 Goals Completed
            if (completedGoals.Count >= 10 && !existingAchievements.Contains("Elite"))
            {
                achievementsToAward.Add(new UserAchievement {
                    UserId = userId,
                    AchievementName = "Elite",
                    Description = "Completed 10 milestones. You're a pro!",
                    Icon = "👑",
                    AchievedAt = DateTime.UtcNow
                });
            }

            // 5. Perfect Month
            var firstDayOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var goalsThisMonth = goals.Where(g => g.EndDate >= firstDayOfMonth && g.EndDate < firstDayOfMonth.AddMonths(1)).ToList();
            if (goalsThisMonth.Count > 0 && goalsThisMonth.All(g => g.ProgressPercent >= 100) && !existingAchievements.Contains("Perfect Month"))
            {
                achievementsToAward.Add(new UserAchievement {
                    UserId = userId,
                    AchievementName = "Perfect Month",
                    Description = "Completed all milestones set for this month!",
                    Icon = "🌟",
                    AchievedAt = DateTime.UtcNow
                });
            }

            // 6. Hat Trick: Streak (3 goals completed in a row)
            var recentGoals = goals.OrderByDescending(g => g.UpdatedAt).Take(3).ToList();
            if (recentGoals.Count == 3 && recentGoals.All(g => g.ProgressPercent >= 100) && !existingAchievements.Contains("Hat Trick"))
            {
                achievementsToAward.Add(new UserAchievement {
                    UserId = userId,
                    AchievementName = "Hat Trick",
                    Description = "Completed 3 milestones in a row!",
                    Icon = "🎩",
                    AchievedAt = DateTime.UtcNow
                });
            }

            if (achievementsToAward.Any())
            {
                _context.UserAchievements.AddRange(achievementsToAward);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<GoalSubTask>> GetSubTasksAsync(int goalId, string userId)
        {
            var goal = await _context.Goals.AnyAsync(g => g.Id == goalId && g.UserId == userId);
            if (!goal) return Enumerable.Empty<GoalSubTask>();

            return await _context.GoalSubTasks
                .Where(s => s.GoalId == goalId)
                .OrderBy(s => s.IsCompleted)
                .ThenByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<GoalSubTask> AddSubTaskAsync(int goalId, string title, string userId)
        {
            var goal = await _context.Goals.AnyAsync(g => g.Id == goalId && g.UserId == userId);
            if (!goal) throw new UnauthorizedAccessException();

            var subTask = new GoalSubTask { GoalId = goalId, Title = title };
            _context.GoalSubTasks.Add(subTask);
            await _context.SaveChangesAsync();
            return subTask;
        }

        public async Task<bool> ToggleSubTaskAsync(int subTaskId, string userId)
        {
            var subTask = await _context.GoalSubTasks
                .Include(s => s.Goal)
                .FirstOrDefaultAsync(s => s.Id == subTaskId && s.Goal!.UserId == userId);

            if (subTask == null) return false;

            subTask.IsCompleted = !subTask.IsCompleted;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteSubTaskAsync(int subTaskId, string userId)
        {
            var subTask = await _context.GoalSubTasks
                .Include(s => s.Goal)
                .FirstOrDefaultAsync(s => s.Id == subTaskId && s.Goal!.UserId == userId);

            if (subTask == null) return false;

            _context.GoalSubTasks.Remove(subTask);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<string>> GetSuggestedSubTasksAsync(int goalId, string userId)
        {
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
            if (goal == null) return Enumerable.Empty<string>();

            return goal.Category switch
            {
                "Health" => new[] { "Plan weekly workout", "Track baseline metrics", "Prepare meal plan", "Join community group" },
                "Work" => new[] { "Define success metrics", "Draft project outline", "Schedule stakeholder review", "Identify bottlenecks" },
                "Learning" => new[] { "Review fundamental concepts", "Source study materials", "Complete practice exercises", "Set weekly study slots" },
                "Finance" => new[] { "Audit current expenses", "Set up automated savings", "Research investment options", "Create monthly budget" },
                _ => new[] { "Break into 5 smaller steps", "Identify required resources", "Set daily deep work block", "Celebrate small wins" }
            };
        }
    }
}
