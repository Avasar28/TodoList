/**
 * Habit Tracker Widget Logic
 * Handles storage, impulse updates, and streak calculations.
 */

const HABIT_STORAGE_KEY = 'habitTrackerData';

class HabitTracker {
    constructor() {
        this.habits = [];
        this.init();
    }

    init() {
        this.loadHabits();
        this.checkDailyReset();
        this.render();
        this.attachEventListeners();
    }

    loadHabits() {
        const stored = localStorage.getItem(HABIT_STORAGE_KEY);
        if (stored) {
            this.habits = JSON.parse(stored);
        } else {
            this.habits = [];
        }
    }

    saveHabits() {
        localStorage.setItem(HABIT_STORAGE_KEY, JSON.stringify(this.habits));
        this.render();
    }

    checkDailyReset() {
        const today = new Date().toLocaleDateString('en-CA'); // YYYY-MM-DD
        let changed = false;

        this.habits.forEach(habit => {
            // Update daily completion flag
            const hasEntryForToday = habit.completedDates.includes(today);
            if (habit.isCompletedToday !== hasEntryForToday) {
                habit.isCompletedToday = hasEntryForToday;
                changed = true;
            }

            // Recalculate streak to ensure it resets if days were missed since last visit
            const oldStreak = habit.currentStreak;
            this.calculateStreaks(habit);
            if (habit.currentStreak !== oldStreak) {
                changed = true;
            }
        });

        if (changed) this.saveHabits();
    }

    addHabit(name) {
        if (!name.trim()) return;

        const newHabit = {
            id: Date.now().toString(),
            name: name.trim(),
            createdAt: new Date().toISOString(),
            completedDates: [],
            currentStreak: 0,
            bestStreak: 0,
            isCompletedToday: false,
            isPaused: false
        };

        this.habits.push(newHabit);
        this.saveHabits();
    }

    deleteHabit(id) {
        if (confirm('Are you sure you want to delete this habit?')) {
            this.habits = this.habits.filter(h => h.id !== id);
            this.saveHabits();
        }
    }

    togglePause(id) {
        const habit = this.habits.find(h => h.id === id);
        if (habit) {
            habit.isPaused = !habit.isPaused;
            this.saveHabits();
        }
    }

    toggleCompletion(id) {
        const habit = this.habits.find(h => h.id === id);
        if (!habit || habit.isPaused) return;

        const today = new Date().toLocaleDateString('en-CA');
        const index = habit.completedDates.indexOf(today);

        if (index > -1) {
            // Remove (Undo)
            habit.completedDates.splice(index, 1);
            habit.isCompletedToday = false;
        } else {
            // Complete
            habit.completedDates.push(today);
            habit.isCompletedToday = true;
        }

        // Recalculate Streaks
        this.calculateStreaks(habit);
        this.saveHabits();
    }

    calculateStreaks(habit) {
        // Sort dates descending
        const dates = [...habit.completedDates].sort((a, b) => new Date(b) - new Date(a));
        if (dates.length === 0) {
            habit.currentStreak = 0;
            return;
        }

        const today = new Date().toLocaleDateString('en-CA');
        const yesterday = new Date(Date.now() - 86400000).toLocaleDateString('en-CA');

        // Check if streak is alive
        // Alive if completed today OR (completed yesterday AND NOT today yet)
        // Actually, for calculation, we just count backwards from today or yesterday.

        let currentStreak = 0;
        let checkDate = new Date();

        // If today is completed, start checking from today.
        // If today is NOT completed, but yesterday IS, start from yesterday.
        // Else streak is 0.

        const hasToday = habit.completedDates.includes(today);
        const hasYesterday = habit.completedDates.includes(yesterday);

        if (hasToday) {
            // Count backwards from today
            currentStreak = this.countConsecutive(dates, today);
        } else if (hasYesterday) {
            // Count backwards from yesterday
            currentStreak = this.countConsecutive(dates, yesterday);
        } else {
            currentStreak = 0;
        }

        habit.currentStreak = currentStreak;
        if (currentStreak > habit.bestStreak) {
            habit.bestStreak = currentStreak;
        }
    }

    countConsecutive(sortedDates, startDateStr) {
        let count = 0;
        let currentDate = new Date(startDateStr);

        while (true) {
            const dateStr = currentDate.toLocaleDateString('en-CA');
            if (sortedDates.includes(dateStr)) {
                count++;
                // Move to previous day
                currentDate.setDate(currentDate.getDate() - 1);
            } else {
                break;
            }
        }
        return count;
    }

    getWeeklyProgress(habit) {
        // Return array of 7 booleans for last 7 days (Today -> T-6)
        const progress = [];
        for (let i = 6; i >= 0; i--) {
            const d = new Date();
            d.setDate(d.getDate() - i);
            const dateStr = d.toLocaleDateString('en-CA');
            progress.push(habit.completedDates.includes(dateStr));
        }
        return progress; // [T-6, T-5, ..., Today]
    }

    render() {
        // 1. Render Detailed List (in Panel)
        const container = document.getElementById('habitList');
        const emptyState = document.getElementById('habitEmptyState');
        // badge in panel header if exists? No, mostly just list.

        if (container) {
            container.innerHTML = '';
            if (this.habits.length === 0) {
                if (emptyState) emptyState.style.display = 'block';
            } else {
                if (emptyState) emptyState.style.display = 'none';
                this.habits.forEach(habit => {
                    const el = document.createElement('div');
                    el.className = `habit-item ${habit.isCompletedToday ? 'completed' : ''} ${habit.isPaused ? 'paused' : ''}`;

                    const weeklyProgress = this.getWeeklyProgress(habit);
                    const progressHtml = weeklyProgress.map(done =>
                        `<span class="progress-dot ${done ? 'done' : ''}"></span>`
                    ).join('');

                    el.innerHTML = `
                        <div class="habit-main">
                            <button class="habit-check-btn" onclick="habitTracker.toggleCompletion('${habit.id}')" ${habit.isPaused ? 'disabled' : ''}>
                                ${habit.isCompletedToday ? '✓' : ''}
                            </button>
                            <div class="habit-info">
                                <span class="habit-name">${habit.name}</span>
                                <div class="habit-stats">
                                    <span class="streak-pill ${habit.currentStreak > 0 ? 'active' : ''}">
                                        🔥 ${habit.currentStreak}
                                    </span>
                                     <span class="best-pill">
                                        🏆 ${habit.bestStreak}
                                    </span>
                                </div>
                            </div>
                        </div>
                        <div class="habit-meta">
                            <div class="weekly-progress">
                                ${progressHtml}
                            </div>
                            <div class="habit-actions">
                                 <button class="action-icon" onclick="habitTracker.togglePause('${habit.id}')" title="${habit.isPaused ? 'Resume' : 'Pause'}">
                                    ${habit.isPaused ? '▶️' : '⏸️'}
                                </button>
                                <button class="action-icon delete" onclick="habitTracker.deleteHabit('${habit.id}')" title="Delete">
                                    🗑️
                                </button>
                            </div>
                        </div>
                    `;
                    container.appendChild(el);
                });
            }
        }

        // 2. Render Summary Card (Dashboard Widget)
        const summaryCount = document.getElementById('habitSummaryCount');
        const summaryBest = document.getElementById('habitSummaryBest');
        const summaryProgress = document.getElementById('habitSummaryProgress');

        if (summaryCount) {
            const activeCount = this.habits.filter(h => !h.isPaused).length;
            summaryCount.textContent = `${activeCount} Active`;
        }

        if (summaryBest) {
            const best = this.habits.reduce((max, h) => Math.max(max, h.currentStreak), 0);
            summaryBest.textContent = best;
        }

        if (summaryProgress) {
            const total = this.habits.filter(h => !h.isPaused).length;
            if (total > 0) {
                const completed = this.habits.filter(h => !h.isPaused && h.isCompletedToday).length;
                const percent = Math.round((completed / total) * 100);
                summaryProgress.style.width = `${percent}%`;
            } else {
                summaryProgress.style.width = '0%';
            }
        }
    }

    attachEventListeners() {
        const addBtn = document.getElementById('addHabitBtn');
        const input = document.getElementById('newHabitInput');

        if (addBtn && input) {
            addBtn.addEventListener('click', () => {
                this.addHabit(input.value);
                input.value = '';
            });
            input.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') {
                    this.addHabit(input.value);
                    input.value = '';
                }
            });
        }
    }
}

// Initialize on load
document.addEventListener('DOMContentLoaded', () => {
    window.habitTracker = new HabitTracker();
});
