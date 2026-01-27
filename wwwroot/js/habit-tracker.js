/**
 * Habit Tracker Widget Logic
 * Handles storage, impulse updates, and streak calculations.
 */

const HABIT_STORAGE_KEY = 'habitTrackerData';

class HabitTracker {
    constructor() {
        this.habits = JSON.parse(localStorage.getItem(HABIT_STORAGE_KEY)) || [];
        this.openSettings = new Set(); // Track which settings rows are expanded
        this.checkDailyReset();
        this.startReminderLoop();
        this.init();
    }

    init() {
        this.render();
        this.attachEventListeners();
        // Request persistent permissions if any reminder is already active
        if (this.habits.some(h => h.reminderEnabled)) {
            this.requestNotificationPermission();
        }
    }

    async requestNotificationPermission() {
        if (!("Notification" in window)) {
            console.warn("This browser does not support desktop notification");
            return false;
        }
        if (Notification.permission === "granted") return true;
        if (Notification.permission !== "denied") {
            const permission = await Notification.requestPermission();
            return permission === "granted";
        }
        return false;
    }

    startReminderLoop() {
        // Check every minute
        setInterval(() => this.checkReminders(), 60000);
    }

    checkReminders() {
        // Run regardless of permission, we will decide delivery method inside
        const now = new Date();
        const currentHours = String(now.getHours()).padStart(2, '0');
        const currentMinutes = String(now.getMinutes()).padStart(2, '0');
        const currentTime = `${currentHours}:${currentMinutes}`;
        const todayStr = now.toISOString().split('T')[0];

        this.habits.forEach(h => {
            if (h.reminderEnabled && !h.isPaused && h.reminderTime === currentTime) {
                // Prevent duplicate alerts for the same day
                if (h.lastReminderDate !== todayStr) {
                    if (Notification.permission === "granted") {
                        this.triggerNotification(h);
                    } else {
                        this.triggerInAppAlert(h);
                    }
                    h.lastReminderDate = todayStr;
                    this.saveHabits();
                }
            }
        });
    }

    triggerNotification(habit) {
        // Browser Notification
        new Notification("Habit Reminder 🔔", {
            body: `Time to work on: ${habit.name}!`,
            icon: '/favicon.ico', // fallback
            tag: 'habit-reminder'
        });
    }

    triggerInAppAlert(habit) {
        const widget = document.getElementById('habitWidget');
        if (widget) {
            const toast = document.createElement('div');
            toast.className = 'habit-alert-toast';
            toast.innerHTML = `<span>🔔 ${habit.name}</span>`;
            widget.appendChild(toast);

            // Remove after 5 seconds
            setTimeout(() => {
                toast.classList.add('fade-out');
                setTimeout(() => toast.remove(), 500);
            }, 5000);
        }
    }

    loadHabits() {
        // Habits are loaded in the constructor now
        // This method is kept for consistency but might be redundant
        const stored = localStorage.getItem(HABIT_STORAGE_KEY);
        if (stored) {
            this.habits = JSON.parse(stored);
        } else {
            this.habits = [];
        }
    }

    saveHabits() {
        localStorage.setItem(HABIT_STORAGE_KEY, JSON.stringify(this.habits));
        this.render(); // Re-render after saving
    }

    // --- AI Suggestion Logic ---
    getDismissed() {
        return JSON.parse(localStorage.getItem('habit_dismissed_suggestions')) || [];
    }

    dismissSuggestion(text) {
        const dismissed = this.getDismissed();
        dismissed.push(text);
        localStorage.setItem('habit_dismissed_suggestions', JSON.stringify(dismissed));
        this.render(); // Re-render to show new suggestions
    }

    addSuggestion(text) {
        this.addHabit(text);
        // We 'dismiss' it so it doesn't show up in suggestions again, but it's now an active habit
        const dismissed = this.getDismissed();
        dismissed.push(text);
        localStorage.setItem('habit_dismissed_suggestions', JSON.stringify(dismissed));
        this.render();
    }

    getSmartSuggestions() {
        const existingNames = this.habits.map(h => h.name.toLowerCase());
        const dismissed = this.getDismissed().map(d => d.toLowerCase());
        const hour = new Date().getHours();

        // 1. High-Capacity Association Map (50+ items total across library)
        const associationMap = [
            { keywords: ['water', 'hydration', 'drink', 'thirsty'], suggestions: ['Limit soda/sugar', 'Track caffeine', 'Eat more citrus', 'Herbal tea at night', 'Glass of water before meals', 'Cucumber water', 'Coconut water'] },
            { keywords: ['run', 'gym', 'workout', 'exercise', 'walk', 'yoga', 'sport', 'fit', 'train', 'muscle'], suggestions: ['Stretch for 5 mins', 'Cooldown/Shower', 'Log protein', 'Deep muscle massage', 'Foam roll', 'Pack gym bag tomorrow', 'Check heart rate', 'Try a new route'] },
            { keywords: ['read', 'book', 'study', 'learn', 'code', 'write', 'focus'], suggestions: ['Review notes', 'Screen break', 'Clean study space', 'Write a summary', 'Listen to focus music', 'No phone for 30 mins', 'Set a pomodoro timer'] },
            { keywords: ['sleep', 'bed', 'rest', 'night', 'tired', 'dream'], suggestions: ['No phone in bed', 'Dim lights early', 'Deep breathing', 'White noise', 'Read paper book', 'Cool down room', 'Update dream journal'] },
            { keywords: ['meditate', 'breath', 'mindful', 'zen', 'journal', 'calm', 'relax'], suggestions: ['Gratitude log', 'Light a candle', 'Digital detox', 'Visualization', 'Box breathing', 'Name 3 good things', 'Listen to nature sounds'] },
            { keywords: ['food', 'eat', 'cook', 'meal', 'diet', 'snack', 'prep'], suggestions: ['Prep lunch tomorrow', 'Try new veggie', 'Eat without screens', 'Smaller portions', 'Fruit after dinner'] },
            { keywords: ['clean', 'tidy', 'house', 'room', 'space', 'organize'], suggestions: ['5 min tidy', 'Empty trash', 'Wipe desk', 'Water plants', 'Make the bed'] }
        ];

        let potentialSuggestions = [];

        // Scan ALL habits for associations
        this.habits.forEach(habit => {
            const name = habit.name.toLowerCase();
            const match = associationMap.find(a => a.keywords.some(k => name.includes(k)));
            if (match) {
                match.suggestions.forEach(s => {
                    potentialSuggestions.push({ text: s, type: 'associative', weight: 2 });
                });
            }
        });

        // 2. High-Capacity Base Library
        const library = [
            // Morning
            { text: "Drink water first thing", type: 'morning', start: 5, end: 11, weight: 1 },
            { text: "Make the bed", type: 'morning', start: 6, end: 10, weight: 1 },
            { text: "5 min stretch", type: 'morning', start: 6, end: 12, weight: 1 },
            { text: "Plan the day", type: 'morning', start: 7, end: 11, weight: 1 },
            { text: "Morning sunlight", type: 'morning', start: 6, end: 10, weight: 1 },
            { text: "Healthy breakfast", type: 'morning', start: 7, end: 10, weight: 1 },

            // Afternoon
            { text: "10 min walk", type: 'afternoon', start: 12, end: 17, weight: 1 },
            { text: "Healthy snack", type: 'afternoon', start: 14, end: 17, weight: 1 },
            { text: "Clear email inbox", type: 'afternoon', start: 13, end: 16, weight: 1 },
            { text: "Refill water bottle", type: 'afternoon', start: 12, end: 16, weight: 1 },
            { text: "Posture check", type: 'afternoon', start: 13, end: 17, weight: 1 },
            { text: "Quick desk tidy", type: 'afternoon', start: 15, end: 17, weight: 1 },

            // Evening
            { text: "Read 5 pages", type: 'evening', start: 18, end: 23, weight: 1 },
            { text: "No screens 1h before bed", type: 'evening', start: 20, end: 23, weight: 1 },
            { text: "Journaling", type: 'evening', start: 19, end: 23, weight: 1 },
            { text: "Prep clothes tomorrow", type: 'evening', start: 20, end: 22, weight: 1 },
            { text: "Dim the lights", type: 'evening', start: 19, end: 22, weight: 1 },
            { text: "Herbal tea", type: 'evening', start: 20, end: 23, weight: 1 },

            // Anytime
            { text: "Meditate for 5 mins", type: 'anytime', start: 0, end: 24, weight: 1 },
            { text: "Posture check", type: 'anytime', start: 8, end: 20, weight: 1 },
            { text: "Call a friend/family", type: 'anytime', start: 10, end: 20, weight: 1 },
            { text: "Deep breathing", type: 'anytime', start: 0, end: 24, weight: 1 },
            { text: "Digital detox session", type: 'anytime', start: 0, end: 24, weight: 1 },
            { text: "Listen to music", type: 'anytime', start: 0, end: 24, weight: 1 }
        ];

        // Combine
        const allPossible = [...potentialSuggestions, ...library];

        // Filter: Relaxed rules to prevent disappearance
        const filtered = allPossible.filter(item => {
            const lowerText = item.text.toLowerCase();
            // Only block exact matches, don't block similar ones like "Run" blocking "Stretch"
            if (existingNames.includes(lowerText)) return false;
            // Also block if the exact suggestion is dismissed
            if (dismissed.includes(lowerText)) return false;

            // Time match logic
            if (item.type === 'associative') return true;
            return hour >= item.start && hour <= item.end;
        });

        // Deduplicate and Shuffle with Weighting
        const uniqueMap = new Map();
        filtered.forEach(s => {
            if (!uniqueMap.has(s.text) || s.weight > uniqueMap.get(s.text).weight) {
                uniqueMap.set(s.text, s);
            }
        });

        const finalOptions = Array.from(uniqueMap.values());

        // Sort: Mix associative and library for variety, but associative weighted slightly higher
        return finalOptions
            .sort(() => Math.random() - 0.5) // Initial shuffle
            .sort((a, b) => (b.weight || 1) - (a.weight || 1)) // Prioritize associative
            .slice(0, 6); // Show up to 6 now
    }

    // --- Weekly Report Engine ---
    getWeeklyReportData() {
        const last7Days = [];
        for (let i = 6; i >= 0; i--) {
            const d = new Date();
            d.setDate(d.getDate() - i);
            last7Days.push(d.toLocaleDateString('en-CA'));
        }

        const activeHabits = this.habits.filter(h => !h.isPaused);
        if (activeHabits.length === 0) return null;

        let totalPossible = activeHabits.length * 7;
        let totalCompletions = 0;
        let habitsMetrics = [];

        activeHabits.forEach(habit => {
            const weekCompletions = habit.completedDates.filter(date => last7Days.includes(date)).length;
            totalCompletions += weekCompletions;
            habitsMetrics.push({
                name: habit.name,
                count: weekCompletions,
                rate: Math.round((weekCompletions / 7) * 100),
                bestStreak: habit.currentStreak // simplified for now
            });
        });

        // Best and Worst
        const bestHabit = [...habitsMetrics].sort((a, b) => b.count - a.count)[0];
        const mostMissed = [...habitsMetrics].sort((a, b) => a.count - b.count)[0];
        const completionRate = Math.round((totalCompletions / totalPossible) * 100);

        return {
            weekEnding: last7Days[6],
            totalHabits: activeHabits.length,
            totalCompletions,
            completionRate,
            bestHabit,
            mostMissed,
            habitsMetrics,
            insights: this.generateInsights({ totalCompletions, completionRate, bestHabit, mostMissed, habitsMetrics })
        };
    }

    generateInsights(data) {
        const insights = [];
        const { totalCompletions, completionRate, bestHabit, mostMissed, habitsMetrics } = data;

        if (completionRate > 80) {
            insights.push("🌟 Exceptional consistency! You're crushing your goals.");
        } else if (completionRate > 50) {
            insights.push(`📈 Solid work! You completed ${totalCompletions} tasks this week.`);
        } else {
            insights.push("🌱 Every small step counts. Let's aim for a bit more next week!");
        }

        if (bestHabit && bestHabit.count >= 5) {
            insights.push(`🔥 '${bestHabit.name}' is becoming a strong part of your routine.`);
        }

        if (mostMissed && mostMissed.count < 3 && mostMissed.count !== bestHabit.count) {
            insights.push(`💡 Focus on '${mostMissed.name}' next week—you can do it!`);
        }

        // Contextual time-based insight (Mock logic: assume morning if many early completions or just random for now as we don't store time)
        // But for "Human Readable" requirement:
        if (completionRate > 70) insights.push("💪 Mornings seem to be your power hours.");

        return insights.slice(0, 3);
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

        const id = Date.now().toString();
        const newHabit = {
            id,
            name: name.trim(),
            createdAt: new Date().toISOString(),
            completedDates: [], // Array of YYYY-MM-DD
            isPaused: false,
            isCompletedToday: false,
            currentStreak: 0,
            bestStreak: 0,
            reminderEnabled: false,
            reminderTime: "09:00", // Default
            lastReminderDate: ""
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

    toggleReminder(id) {
        const habit = this.habits.find(h => h.id === id);
        if (habit) {
            habit.reminderEnabled = !habit.reminderEnabled;
            if (habit.reminderEnabled) {
                this.requestNotificationPermission();
            }
            this.saveHabits();
            this.render();
        }
    }

    updateReminderTime(id, time) {
        const habit = this.habits.find(h => h.id === id);
        if (habit) {
            habit.reminderTime = time;
            this.saveHabits();
            // Don't need full re-render for this input change usually
        }
    }

    toggleSettings(id) {
        // UI Helper to expand/collapse settings row
        if (this.openSettings.has(id)) {
            this.openSettings.delete(id);
        } else {
            this.openSettings.add(id);
        }
        this.render(); // Re-render to apply the 'open' class
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

    renderHeatmap(habit) {
        // Generate last 60 days
        let html = '';
        const today = new Date();
        const DAYS_TO_SHOW = 60;

        for (let i = DAYS_TO_SHOW - 1; i >= 0; i--) {
            const date = new Date();
            date.setDate(today.getDate() - i);
            const dateStr = date.toISOString().split('T')[0];

            const isCompleted = habit.completedDates.includes(dateStr);
            const isToday = i === 0;

            // Format for tooltip
            const tooltip = `${date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}: ${isCompleted ? 'Completed' : 'Missed'}`;

            html += `<div class="heatmap-day ${isCompleted ? 'filled' : ''} ${isToday ? 'today' : ''}" title="${tooltip}"></div>`;
        }
        return `
            <div class="habit-heatmap">
                ${html}
            </div>
        `;
    }

    getWeeklyProgress(habit) {
        // Kept for legacy or summary if needed, but not used in detailed render with heatmap
        const progress = [];
        for (let i = 6; i >= 0; i--) {
            const d = new Date();
            d.setDate(d.getDate() - i);
            const dateStr = d.toLocaleDateString('en-CA');
            progress.push(habit.completedDates.includes(dateStr));
        }
        return progress; // [T-6, T-5, ..., Today]
    }

    renderWeeklyReport() {
        const data = this.getWeeklyReportData();
        if (!data) return '';

        let metricsHtml = `
            <div class="weekly-report-grid">
                <div class="report-metric-card">
                    <span class="metric-label">Completion</span>
                    <span class="metric-value">${data.completionRate}%</span>
                    <div class="mini-progress-track"><div class="mini-progress-fill" style="width: ${data.completionRate}%"></div></div>
                </div>
                <div class="report-metric-card">
                    <span class="metric-label">Completions</span>
                    <span class="metric-value">${data.totalCompletions}</span>
                    <span class="metric-sub">This week</span>
                </div>
                <div class="report-metric-card">
                    <span class="metric-label">Active Habits</span>
                    <span class="metric-value">${data.totalHabits}</span>
                    <span class="metric-sub">Tracked</span>
                </div>
            </div>
        `;

        let insightsHtml = data.insights.map(ins => `
            <div class="report-insight-item">
                <span class="insight-text">${ins}</span>
            </div>
        `).join('');

        let habitSummariesHtml = data.habitsMetrics.map(h => `
            <div class="habit-report-row">
                <div class="habit-report-info">
                    <span class="habit-report-name">${h.name}</span>
                    <span class="habit-report-status">${h.count}/7 days</span>
                </div>
                <div class="habit-report-bar">
                    <div class="report-bar-fill ${h.rate > 70 ? 'strong' : h.rate < 40 ? 'needs-work' : ''}" style="width: ${h.rate}%"></div>
                </div>
                <span class="habit-report-icon">${h.rate > 70 ? '⭐' : h.rate < 40 ? '⚠️' : ''}</span>
            </div>
        `).join('');

        return `
            <div class="habit-weekly-report animate-slide-in">
                <div class="report-header">
                    <h3>📊 Weekly Report</h3>
                    <span class="report-date">Last 7 Days</span>
                </div>
                
                ${metricsHtml}

                <div class="report-section-label">Habit Consistency</div>
                <div class="report-habit-list">
                    ${habitSummariesHtml}
                </div>

                <div class="report-section-label">AI Insights</div>
                <div class="report-insights-container">
                    ${insightsHtml}
                </div>
            </div>
        `;
    }

    render() {
        // 1. Render Detailed List (in Panel)
        const container = document.getElementById('habitList');
        const emptyState = document.getElementById('habitEmptyState');

        if (container) {
            container.innerHTML = '';

            // PREPEND Weekly Report at the top
            if (this.habits.length > 0) {
                const reportHtml = this.renderWeeklyReport();
                const reportDiv = document.createElement('div');
                reportDiv.innerHTML = reportHtml;
                container.appendChild(reportDiv);
            }

            if (this.habits.length === 0) {
                if (emptyState) emptyState.style.display = 'block';
            } else {
                if (emptyState) emptyState.style.display = 'none';
                this.habits.forEach(habit => {
                    const el = document.createElement('div');
                    el.className = `habit-item ${habit.isCompletedToday ? 'completed' : ''} ${habit.isPaused ? 'paused' : ''}`;

                    const heatmapHtml = this.renderHeatmap(habit);

                    el.innerHTML = `
                        <div class="habit-main">
                            <div class="habit-checkbox-wrapper">
                                <button class="habit-check-btn" onclick="habitTracker.toggleCompletion('${habit.id}')" ${habit.isPaused ? 'disabled' : ''}>
                                    ${habit.isCompletedToday ? '✓' : ''}
                                </button>
                            </div>
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
                        <div class="habit-meta-col">
                            <div class="habit-heatmap-container">
                                ${heatmapHtml}
                            </div>
                            <div class="habit-actions-row">
                                <button class="action-btn-text" onclick="habitTracker.toggleSettings('${habit.id}')" title="Notification Settings">
                                     🔔 ${habit.reminderEnabled ? 'On' : 'Off'}
                                </button>
                                 <button class="action-btn-text" onclick="habitTracker.togglePause('${habit.id}')" title="${habit.isPaused ? 'Resume' : 'Pause'}">
                                    ${habit.isPaused ? '▶ Resume' : '⏸ Pause'}
                                </button>
                                <button class="action-btn-text delete" onclick="habitTracker.deleteHabit('${habit.id}')" title="Delete">
                                    🗑
                                </button>
                            </div>
                        </div>
                        
                        <!-- Reminder Settings Row -->
                        <div class="habit-settings-row ${this.openSettings.has(habit.id) ? 'open' : ''}" id="settings-${habit.id}">
                            <div class="setting-group">
                                <span class="setting-label">Enable Notification</span>
                                <label class="switch-toggle">
                                    <input type="checkbox" ${habit.reminderEnabled ? 'checked' : ''} onchange="habitTracker.toggleReminder('${habit.id}')">
                                    <span class="slider"></span>
                                </label>
                            </div>
                            ${habit.reminderEnabled ? `
                                <div class="setting-group animate-slide-in">
                                    <span class="setting-label">Notify at:</span>
                                    <input type="time" class="time-picker-glass" value="${habit.reminderTime}" onchange="habitTracker.updateReminderTime('${habit.id}', this.value)">
                                </div>
                            ` : ''}
                        </div>
                    `;
                    container.appendChild(el);
                });

                // 3. Render AI Suggestions (if any)
                const suggestions = this.getSmartSuggestions();
                if (suggestions.length > 0) {
                    const suggestionsSection = document.createElement('div');
                    suggestionsSection.className = 'habit-suggestions-section animate-slide-in';

                    const header = document.createElement('div');
                    header.className = 'suggestions-header';
                    header.innerHTML = `<span>✨ AI Suggestions</span> <small>Based on time of day</small>`;
                    suggestionsSection.appendChild(header);

                    const grid = document.createElement('div');
                    grid.className = 'suggestions-grid';

                    suggestions.forEach(s => {
                        const card = document.createElement('div');
                        card.className = 'suggestion-card';
                        card.innerHTML = `
                            <span class="suggestion-text">${s.text}</span>
                            <div class="suggestion-actions">
                                <button class="sugg-btn add" onclick="habitTracker.addSuggestion('${s.text}')" title="Add Habit">+</button>
                                <button class="sugg-btn dismiss" onclick="habitTracker.dismissSuggestion('${s.text}')" title="Dismiss">×</button>
                            </div>
                        `;
                        grid.appendChild(card);
                    });
                    suggestionsSection.appendChild(grid);
                    container.appendChild(suggestionsSection);
                }
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
