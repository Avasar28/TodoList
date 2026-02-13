/**
 * Holiday Calendar Widget Logic
 * Handles country search, year selection, and fetching/rendering holidays.
 */

const HolidayWidget = {
    allCountries: [],
    selectedCountryCode: '',
    selectedYear: new Date().getFullYear(),
    selectedCountryName: '', // Cache name for header
    currentView: 'list', // 'list' or 'calendar'
    calendarMonth: new Date().getMonth(), // 0-indexed
    calendarYear: new Date().getFullYear(),
    holidaysData: null, // Cache for the current country/year
    reminders: JSON.parse(localStorage.getItem('holidayReminders')) || [],
    dismissedSuggestions: JSON.parse(localStorage.getItem('dismissedHolidaySuggestions')) || [],
    manualSelection: false // Flag to prevent weather sync from overwriting user choice
};

HolidayWidget.init = function () {
    console.log("HolidayWidget: Initializing (Refactored)...", this);

    this.calendarMonth = new Date().getMonth();
    this.calendarYear = new Date().getFullYear();

    // Safety check for cacheCountries
    if (typeof this.cacheCountries === 'function') {
        this.cacheCountries();
    } else {
        console.error("HolidayWidget: cacheCountries is NOT a function!", this.cacheCountries);
    }

    this.attachEventListeners();

    // Initial sync check: if weather data is already in global scope or status bar
    this.fetchDefaultHolidays();
    this.startReminderLoop();
};

HolidayWidget.requestNotificationPermission = async function () {
    if (!("Notification" in window)) return "unsupported";
    if (Notification.permission === "granted") return "granted";
    if (Notification.permission !== "denied") {
        const permission = await Notification.requestPermission();
        return permission;
    }
    return "denied";
};

HolidayWidget.cacheCountries = async function () {
    try {
        const response = await fetch('/Todo/GetAvailableCountriesJson');
        if (response.ok) {
            this.allCountries = await response.json();
            console.log(`HolidayWidget: Cached ${this.allCountries.length} countries.`);
        }
    } catch (error) {
        console.error("HolidayWidget: Error caching countries", error);
    }
};

HolidayWidget.attachEventListeners = function () {
    // 1. Search Logic
    const searchInput = document.getElementById('holidayCountrySearch');
    const optionsContainer = document.getElementById('holidayCountryOptions');
    const customSelect = document.querySelector('.custom-holiday-select'); // Isolated class

    if (searchInput) {
        searchInput.addEventListener('input', (e) => this.handleSearchInput(e.target.value));
        searchInput.addEventListener('focus', () => {
            if (searchInput.value.length >= 2) {
                if (customSelect) customSelect.classList.add('active');
            }
        });

        // Enter key support
        searchInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                const query = searchInput.value.trim();
                if (query.length >= 2) {
                    this.selectBestMatch(query);
                }
            }
        });
    }

    // Close dropdown when clicking outside
    document.addEventListener('click', (e) => {
        if (customSelect && !customSelect.contains(e.target)) {
            customSelect.classList.remove('active');
        }
    });

    // 2. Year Selection
    const yearSelect = document.getElementById('holidayYearSelect');
    if (yearSelect) {
        yearSelect.addEventListener('change', (e) => {
            this.selectedYear = parseInt(e.target.value);
            this.fetchHolidays();
        });
    }
};

HolidayWidget.handleSearchInput = function (query) {
    const customSelect = document.querySelector('.custom-holiday-select');

    if (!query || query.length < 2) {
        if (customSelect) customSelect.classList.remove('active');
        return;
    }

    const lowerQuery = query.toLowerCase();
    const matches = this.allCountries.filter(c =>
        c.name.toLowerCase().includes(lowerQuery) ||
        c.countryCode.toLowerCase().includes(lowerQuery)
    );

    if (matches.length > 0) {
        this.renderSearchResults(matches);
        if (customSelect) customSelect.classList.add('active');
    } else {
        if (customSelect) customSelect.classList.remove('active');
    }
};

HolidayWidget.selectBestMatch = function (query) {
    const lowerQuery = query.toLowerCase();

    // 1. Exact Name Match
    let match = this.allCountries.find(c => c.name.toLowerCase() === lowerQuery);

    // 2. Exact Code Match
    if (!match) {
        match = this.allCountries.find(c => c.countryCode.toLowerCase() === lowerQuery);
    }

    // 3. Starts With
    if (!match) {
        match = this.allCountries.find(c => c.name.toLowerCase().startsWith(lowerQuery));
    }

    // 4. Includes
    if (!match) {
        match = this.allCountries.find(c => c.name.toLowerCase().includes(lowerQuery));
    }

    if (match) {
        this.selectCountry(match.countryCode, match.name);
        // Hide dropdown
        const customSelect = document.querySelector('.custom-holiday-select');
        if (customSelect) customSelect.classList.remove('active');
        document.getElementById('holidayCountrySearch').blur();
    }
};

HolidayWidget.renderSearchResults = function (countries) {
    const container = document.getElementById('holidayCountryOptions');
    if (!container) return;

    container.innerHTML = countries.map(c => `
        <div class="option-item" onclick="HolidayWidget.selectCountry('${c.countryCode}', '${c.name.replace(/'/g, "\\'")}')">
            <span class="opt-flag">${this.getFlagEmoji(c.countryCode)}</span>
            <span class="opt-country">${c.name}</span>
        </div>
    `).join('');
};

HolidayWidget.getFlagEmoji = function (countryCode) {
    const codePoints = countryCode
        .toUpperCase()
        .split('')
        .map(char => 127397 + char.charCodeAt());
    return String.fromCodePoint(...codePoints);
};

HolidayWidget.startReminderLoop = function () {
    // Check every minute
    setInterval(() => this.checkReminders(), 60000);
    this.checkReminders(); // Initial check
};

HolidayWidget.checkReminders = function () {
    if (this.reminders.length === 0) return;

    const now = new Date();
    const todayStr = now.toISOString().split('T')[0];

    this.reminders.forEach(r => {
        if (r.notified) return;

        // Calculate reminder date
        const holidayDate = new Date(r.holidayDate);
        const notifyDate = new Date(holidayDate);
        notifyDate.setDate(holidayDate.getDate() - r.reminderOffset);

        const notifyStr = notifyDate.toISOString().split('T')[0];

        if (todayStr === notifyStr) {
            this.showNotification(`Upcoming Holiday: ${r.holidayName}`, ` is in ${r.reminderOffset} day(s)!`);
            r.notified = true;
            this.saveReminders();
        }
    });
};

HolidayWidget.showNotification = async function (title, body) {
    const permission = await this.requestNotificationPermission();
    if (permission === 'granted') {
        new Notification(title, { body, icon: '/favicon.ico' });
    }
};

HolidayWidget.toggleReminder = function (name, date) {
    const existingIndex = this.reminders.findIndex(r => r.holidayName === name && r.holidayDate === date);

    if (existingIndex >= 0) {
        // Remove
        this.reminders.splice(existingIndex, 1);
    } else {
        // Add
        this.reminders.push({
            holidayName: name,
            holidayDate: date,
            reminderOffset: 1, // Default 1 day before
            notified: false
        });
        this.requestNotificationPermission();
    }

    this.saveReminders();
    // Re-render to update UI (simplest way, though wasteful)
    if (this.holidaysData) {
        this.renderHolidays(this.holidaysData); // Rerender to show bell status
    }
};

HolidayWidget.setReminderOffset = function (name, date, offset) {
    const reminder = this.reminders.find(r => r.holidayName === name && r.holidayDate === date);
    if (reminder) {
        reminder.reminderOffset = parseInt(offset);
        reminder.notified = false; // Reset notification status if changed
        this.saveReminders();
    }
};

HolidayWidget.saveReminders = function () {
    localStorage.setItem('holidayReminders', JSON.stringify(this.reminders));
};

HolidayWidget.saveDismissedSuggestions = function () {
    localStorage.setItem('dismissedHolidaySuggestions', JSON.stringify(this.dismissedSuggestions));
};

HolidayWidget.syncWithWeather = function (countryName, countryCode) {
    if (!countryCode) return;

    // If user manually selected a country, do NOT overwrite it with weather sync
    if (this.manualSelection) {
        console.log("HolidayWidget: Sync ignored due to manual selection.");
        return;
    }

    console.log(`HolidayWidget: Syncing with Weather -> ${countryName} (${countryCode})`);

    // If we already have a selection and it's different, update it
    if (this.selectedCountryCode !== countryCode) {
        this.selectCountryByCode(countryCode, countryName, true);
    }
};

HolidayWidget.selectCountryByCode = function (code, fallbackName, isSync = false) {
    if (!code) return;

    // Find country in our cached list for proper name/flag
    // If allCountries is empty (not cached yet), we might have an issue.
    // Ideally we should wait, but for now we proceed.
    const country = this.allCountries.find(c => c.countryCode === code);
    if (country) {
        this.selectCountry(country.countryCode, country.name, isSync);
    } else if (fallbackName) {
        this.selectCountry(code, fallbackName, isSync);
    }
};

HolidayWidget.selectCountry = function (code, name, isSync = false) {
    this.selectedCountryCode = code;
    this.selectedCountryName = name; // Cache name for header

    // If this is a manual selection (not sync), set the flag
    if (!isSync) {
        this.manualSelection = true;
    }

    const searchInput = document.getElementById('holidayCountrySearch');
    const select = document.getElementById('holidayCountrySelect');

    if (searchInput) searchInput.value = name;
    if (select) select.value = code;

    document.querySelectorAll('.custom-holiday-select').forEach(el => el.classList.remove('active'));
    this.fetchHolidays();
};

HolidayWidget.setView = function (view) {
    this.currentView = view;

    // Update pills
    const listBtn = document.getElementById('holidayListViewBtn');
    const calBtn = document.getElementById('holidayCalendarViewBtn');
    const nav = document.getElementById('holidayCalendarNav');

    if (listBtn) listBtn.classList.toggle('active', view === 'list');
    if (calBtn) calBtn.classList.toggle('active', view === 'calendar');
    if (nav) nav.style.display = view === 'calendar' ? 'flex' : 'none';

    if (this.holidaysData) {
        this.renderHolidays(this.holidaysData);
    }
};

HolidayWidget.previousMonth = function () {
    this.calendarMonth--;
    if (this.calendarMonth < 0) {
        this.calendarMonth = 11;
        this.calendarYear--;
    }
    this.updateCalendarView();
};

HolidayWidget.nextMonth = function () {
    this.calendarMonth++;
    if (this.calendarMonth > 11) {
        this.calendarMonth = 0;
        this.calendarYear++;
    }
    this.updateCalendarView();
};

HolidayWidget.updateCalendarView = function () {
    if (this.currentView === 'calendar' && this.holidaysData) {
        this.renderHolidays(this.holidaysData);
    }
};

HolidayWidget.fetchHolidays = async function () {
    if (!this.selectedCountryCode) return;

    const loadingState = document.getElementById('holidayLoadingState');
    const listContainer = document.getElementById('holidayList');
    const card = document.getElementById('holidayWidgetCard');

    if (loadingState) loadingState.style.display = 'flex';
    if (listContainer) listContainer.style.opacity = '0.5';
    if (card) card.classList.add('loading');

    try {
        const response = await fetch(`/Todo/GetHolidaysJson?countryCode=${this.selectedCountryCode}&year=${this.selectedYear}`);

        if (!response.ok) {
            throw new Error(`Server returned ${response.status}`);
        }

        const contentType = response.headers.get("content-type");
        if (!contentType || !contentType.includes("application/json")) {
            throw new Error("Invalid response format (not JSON)");
        }

        const data = await response.json();
        this.holidaysData = data; // Cache
        this.renderHolidays(data);
        this.updateSummaryCard(data);
    } catch (error) {
        console.error("HolidayWidget: Error fetching holidays", error);
        if (listContainer) {
            listContainer.innerHTML = `
                <div class="initial-search-state">
                    <div class="search-icon-placeholder">⚠️</div>
                    <p>Error loading holiday data. Please try again later.</p>
                </div>
            `;
        }
    } finally {
        if (loadingState) loadingState.style.display = 'none';
        if (listContainer) listContainer.style.opacity = '1';
        if (card) card.classList.remove('loading');
    }
};

HolidayWidget.renderHolidays = function (data) {
    const container = document.getElementById('holidayList');
    if (!container) return;

    if (!data.holidays || data.holidays.length === 0) {
        container.innerHTML = `
            <div class="initial-search-state">
                <div class="search-icon-placeholder">📅</div>
                <p>No holiday data available for ${this.selectedCountryName || data.countryCode} in ${data.year}.</p>
                <div class="api-tip" style="margin-top: 1rem; padding: 1rem; background: rgba(255,255,255,0.05); border-radius: 8px; font-size: 0.85rem; color: rgba(255,255,255,0.7);">
                    <p style="margin:0;">💡 <strong>Pro Tip:</strong> Using free data source (~100 countries). To unlock 230+ countries (including India), add a free <a href="https://calendarific.com/signup" target="_blank" style="color: #00d2ff; text-decoration: underline;">Calendarific API Key</a> to <code>appsettings.json</code>.</p>
                </div>
            </div>
        `;
        return;
    }

    if (this.currentView === 'calendar') {
        this.renderCalendarView(data);
        return;
    }

    // Analyze for AI Suggestions
    this.analyzeHolidaysForSuggestions(data);

    // 1. Calculate relative time and categorize
    const now = new Date();
    now.setHours(0, 0, 0, 0);

    const holidaysWithMeta = data.holidays.map(h => {
        const hDate = new Date(h.date);
        hDate.setHours(0, 0, 0, 0);
        const diffTime = hDate.getTime() - now.getTime();
        const daysUntil = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

        let category = 'future';
        if (daysUntil < 0) category = 'past';
        else if (daysUntil <= 30) category = 'upcoming';

        return { ...h, daysUntil, category };
    });

    // 2. Separate Upcoming for the top section
    const upcomingHolidays = holidaysWithMeta
        .filter(h => h.category === 'upcoming')
        .sort((a, b) => a.daysUntil - b.daysUntil);

    // 3. Group the rest by Month for the full calendar
    const sortedHolidays = [...holidaysWithMeta].sort((a, b) => new Date(a.date) - new Date(b.date));
    const monthGrouped = {};
    sortedHolidays.forEach(h => {
        const m = new Date(h.date).toLocaleString('default', { month: 'long' });
        if (!monthGrouped[m]) monthGrouped[m] = [];
        monthGrouped[m].push(h);
    });

    // 4. Build HTML
    let html = `
        <div class="holiday-results-header">
            <h3>${this.selectedCountryName || data.countryCode} - ${data.year}</h3>
            <span class="holiday-total-badge">${data.totalCount} Total Holidays</span>
        </div>
    `;

    // Render Upcoming Highlights if any
    if (upcomingHolidays.length > 0) {
        html += `
            <div class="upcoming-highlights-section">
                <h4 class="section-divider-title">✨ Upcoming Highlights</h4>
                <div class="month-holidays">
                    ${upcomingHolidays.map(h => this.renderHolidayCard(h, true)).join('')}
                </div>
            </div>
        `;
    }

    html += `<h4 class="section-divider-title">📅 Full Calendar</h4>`;

    for (const month in monthGrouped) {
        html += `
            <div class="holiday-month-group">
                <h4 class="month-name">${month}</h4>
                <div class="month-holidays">
                    ${monthGrouped[month].map(h => this.renderHolidayCard(h)).join('')}
                </div>
            </div>
        `;
    }

    container.innerHTML = html;
};

HolidayWidget.renderHolidayCard = function (h, isHighlight = false) {
    const dt = new Date(h.date);
    const day = dt.getDate();
    const monthShort = dt.toLocaleString('default', { month: 'short' });
    const reminder = this.reminders.find(r => r.holidayName === h.name && r.holidayDate === h.date);
    const isActive = !!reminder;

    let cardClass = `holiday-item-card`;
    if (isActive) cardClass += ' has-reminder';
    if (h.category === 'upcoming') cardClass += ' is-upcoming';
    if (h.category === 'past') cardClass += ' is-past';
    if (isHighlight) cardClass += ' highlight-card';

    const badgeHtml = this.getHolidayBadge(h.daysUntil);

    return `
        <div class="${cardClass}">
            <div class="holiday-info">
                <div class="h-name-row">
                    <span class="holiday-name">${h.name}</span>
                    <button class="reminder-toggle-btn ${isActive ? 'active' : ''}" 
                            onclick="HolidayWidget.toggleReminder('${h.name.replace(/'/g, "\\'")}', '${h.date}')"
                            title="${isActive ? 'Disable Reminder' : 'Set Reminder'}">
                        ${isActive ? '🔔' : '🔕'}
                    </button>
                </div>
                <span class="holiday-local-name">${h.localName}</span>
                
                ${badgeHtml ? `<div class="holiday-timing-badge">${badgeHtml}</div>` : ''}

                ${isActive ? `
                    <div class="reminder-config">
                        <label>Remind me:</label>
                        <select onchange="HolidayWidget.setReminderOffset('${h.name.replace(/'/g, "\\'")}', '${h.date}', this.value)">
                            <option value="1" ${reminder.reminderOffset === 1 ? 'selected' : ''}>1 day before</option>
                            <option value="3" ${reminder.reminderOffset === 3 ? 'selected' : ''}>3 days before</option>
                            <option value="7" ${reminder.reminderOffset === 7 ? 'selected' : ''}>1 week before</option>
                        </select>
                    </div>
                ` : ''}
            </div>
            <div class="holiday-date-badge">
                <span class="h-day">${day}</span>
                <span class="h-month">${monthShort}</span>
                <span class="h-dow">${h.dayOfWeek}</span>
            </div>
        </div>
    `;
};

HolidayWidget.getHolidayBadge = function (daysUntil) {
    if (daysUntil < 0) return null;
    if (daysUntil === 0) return `<span class="h-badge today">Today</span>`;
    if (daysUntil === 1) return `<span class="h-badge tomorrow">Tomorrow</span>`;
    if (daysUntil <= 7) return `<span class="h-badge this-week">In ${daysUntil} days</span>`;
    if (daysUntil <= 30) return `<span class="h-badge upcoming">In ${daysUntil} days</span>`;
    return null;
};

HolidayWidget.renderCalendarView = function (data) {
    const container = document.getElementById('holidayList');
    const monthYearLabel = document.getElementById('holidayCalendarMonthYear');

    // Update label
    const monthName = new Date(this.calendarYear, this.calendarMonth).toLocaleString('default', { month: 'long' });
    if (monthYearLabel) monthYearLabel.textContent = `${monthName} ${this.calendarYear}`;

    // Get holidays for this specific month
    const currentMonthHolidays = data.holidays.filter(h => {
        const d = new Date(h.date);
        return d.getMonth() === this.calendarMonth && d.getFullYear() === this.calendarYear;
    });

    // Generate grid
    const firstDay = new Date(this.calendarYear, this.calendarMonth, 1).getDay(); // 0 is Sunday
    const daysInMonth = new Date(this.calendarYear, this.calendarMonth + 1, 0).getDate();

    let html = `
        <div class="calendar-view-container">
            <div class="calendar-grid-header">
                <div>Sun</div><div>Mon</div><div>Tue</div><div>Wed</div><div>Thu</div><div>Fri</div><div>Sat</div>
            </div>
            <div class="calendar-grid-body">
    `;

    // Empty cells for first week
    for (let i = 0; i < firstDay; i++) {
        html += `<div class="calendar-day empty"></div>`;
    }

    const now = new Date();
    now.setHours(0, 0, 0, 0);

    for (let d = 1; d <= daysInMonth; d++) {
        const dateObj = new Date(this.calendarYear, this.calendarMonth, d);
        dateObj.setHours(0, 0, 0, 0);

        const isToday = dateObj.getTime() === now.getTime();
        const isPast = dateObj.getTime() < now.getTime();

        // Find holidays on this date
        // Format to YYYY-MM-DD manually to avoid timezone shifts
        const monthStr = (this.calendarMonth + 1).toString().padStart(2, '0');
        const dayStr = d.toString().padStart(2, '0');
        const targetDate = `${this.calendarYear}-${monthStr}-${dayStr}`;

        const holidaysToday = currentMonthHolidays.filter(h => h.date === targetDate);
        const hasHoliday = holidaysToday.length > 0;

        html += `
            <div class="calendar-day ${isToday ? 'today' : ''} ${isPast ? 'past' : ''} ${hasHoliday ? 'has-holiday' : ''}"
                 ${hasHoliday ? `data-holidays='${JSON.stringify(holidaysToday).replace(/'/g, "&apos;")}' onclick="HolidayWidget.showHolidayTooltip(this)"` : ''}>
                <span class="day-number">${d}</span>
                ${hasHoliday ? `
                    <div class="holiday-markers">
                        <span class="holiday-dot"></span>
                    </div>
                ` : ''}
            </div>
        `;
    }

    html += `</div></div>`;
    container.innerHTML = html;
};

HolidayWidget.showHolidayTooltip = function (element) {
    const holidays = JSON.parse(element.getAttribute('data-holidays'));

    // Remove existing tooltips
    this.hideHolidayTooltip();

    const tooltip = document.createElement('div');
    tooltip.id = 'holidayCalendarTooltip';
    tooltip.className = 'holiday-tooltip-panel';

    let hHtml = holidays.map(h => `
        <div class="tooltip-holiday-item">
            <div class="t-name">${h.name}</div>
            <div class="t-local">${h.localName}</div>
            <div class="t-type">${h.type || 'Public Holiday'}</div>
        </div>
    `).join('<hr class="tooltip-hr" />');

    const dt = new Date(holidays[0].date);
    const dateStr = dt.toLocaleDateString('default', { weekday: 'long', month: 'long', day: 'numeric' });

    tooltip.innerHTML = `
        <div class="tooltip-header">${dateStr}</div>
        <div class="tooltip-body">${hHtml}</div>
        <div class="tooltip-arrow"></div>
    `;

    document.body.appendChild(tooltip);

    // Position tooltip
    const rect = element.getBoundingClientRect();
    tooltip.style.left = `${rect.left + rect.width / 2 - tooltip.offsetWidth / 2}px`;
    tooltip.style.top = `${rect.top - tooltip.offsetHeight - 10}px`;

    // Close on click outside
    const closeHandler = (e) => {
        if (!tooltip.contains(e.target) && e.target !== element) {
            this.hideHolidayTooltip();
            document.removeEventListener('click', closeHandler);
        }
    };
    setTimeout(() => document.addEventListener('click', closeHandler), 10);
};

HolidayWidget.hideHolidayTooltip = function () {
    const existing = document.getElementById('holidayCalendarTooltip');
    if (existing) existing.remove();
};

HolidayWidget.analyzeHolidaysForSuggestions = function (data) {
    if (!data.holidays || data.holidays.length === 0) return;

    const now = new Date();
    now.setHours(0, 0, 0, 0);
    const sixtyDaysFromNow = new Date(now.getTime() + (60 * 24 * 60 * 60 * 1000));

    const suggestions = [];

    // 1. Long Weekend Detection (Fri/Mon)
    data.holidays.forEach(h => {
        const hDate = new Date(h.date);
        if (hDate < now || hDate > sixtyDaysFromNow) return;

        const dayOfWeek = hDate.getDay(); // 0: Sun, 1: Mon, ..., 5: Fri, 6: Sat
        const suggestionId = `long-weekend-${h.name}-${h.date}`;

        if (this.dismissedSuggestions.includes(suggestionId)) return;

        if (dayOfWeek === 5) { // Friday
            suggestions.push({
                id: suggestionId,
                type: 'travel',
                text: `Looks like a 3-day weekend for <strong>${h.name}</strong> (Friday)! Perfect for a short getaway.`
            });
        } else if (dayOfWeek === 1) { // Monday
            suggestions.push({
                id: suggestionId,
                type: 'travel',
                text: `Enjoy a 3-day weekend with <strong>${h.name}</strong> (Monday)! Ideal for a quick trip.`
            });
        }
    });

    // 2. Mid-week Break Detection (Tue/Wed/Thu)
    data.holidays.forEach(h => {
        const hDate = new Date(h.date);
        if (hDate < now || hDate > sixtyDaysFromNow) return;

        const dayOfWeek = hDate.getDay();
        const suggestionId = `midweek-${h.name}-${h.date}`;

        if (this.dismissedSuggestions.includes(suggestionId)) return;

        if (dayOfWeek >= 2 && dayOfWeek <= 4) {
            suggestions.push({
                id: suggestionId,
                type: 'rest',
                text: `Mid-week break on <strong>${h.name}</strong> (${hDate.toLocaleDateString('default', { weekday: 'long' })})! A good chance for some mid-week rest.`
            });
        }
    });

    // 3. Holiday Cluster Detection (2+ holidays in 7 days)
    // Sort holidays first
    const sortedHolidays = [...data.holidays].sort((a, b) => new Date(a.date) - new Date(b.date));
    for (let i = 0; i < sortedHolidays.length - 1; i++) {
        const h1 = sortedHolidays[i];
        const h2 = sortedHolidays[i + 1];
        const d1 = new Date(h1.date);
        const d2 = new Date(h2.date);

        if (d1 < now || d1 > sixtyDaysFromNow) continue;

        const diffDays = Math.ceil((d2 - d1) / (1000 * 60 * 60 * 24));
        if (diffDays > 0 && diffDays <= 7) {
            const suggestionId = `cluster-${h1.name}-${h2.name}`;
            if (this.dismissedSuggestions.includes(suggestionId)) continue;

            suggestions.push({
                id: suggestionId,
                type: 'family',
                text: `A festive cluster! <strong>${h1.name}</strong> and <strong>${h2.name}</strong> are just days apart. Great time for family activities.`
            });
            break; // Only show one cluster suggestion to avoid spam
        }
    }

    this.renderAISuggestions(suggestions.slice(0, 3)); // Show max 3
};

HolidayWidget.renderAISuggestions = function (suggestions) {
    const container = document.getElementById('aiHolidaySuggestions');
    if (!container) return;

    if (suggestions.length === 0) {
        container.style.display = 'none';
        return;
    }

    container.style.display = 'block';
    container.innerHTML = `
        <div class="ai-suggestions-header">
            <span class="ai-sparkle">✨</span>
            <h4>AI Planning Suggestions</h4>
        </div>
        <div class="ai-suggestions-list">
            ${suggestions.map(s => `
                <div class="ai-suggestion-card" data-id="${s.id}">
                    <div class="s-content">
                        <span class="s-icon">${s.type === 'travel' ? '✈️' : s.type === 'rest' ? '🛋️' : '👨‍👩‍👧‍👦'}</span>
                        <p>${s.text}</p>
                    </div>
                    <button class="s-dismiss" onclick="HolidayWidget.dismissSuggestion('${s.id}')" title="Dismiss">×</button>
                </div>
            `).join('')}
        </div>
    `;
};

HolidayWidget.dismissSuggestion = function (suggestionId) {
    if (!this.dismissedSuggestions.includes(suggestionId)) {
        this.dismissedSuggestions.push(suggestionId);
        this.saveDismissedSuggestions();
    }

    const card = document.querySelector(`.ai-suggestion-card[data-id="${suggestionId}"]`);
    if (card) {
        card.classList.add('dismissing');
        setTimeout(() => {
            if (this.holidaysData) {
                this.analyzeHolidaysForSuggestions(this.holidaysData);
            }
        }, 300);
    }
};

HolidayWidget.updateSummaryCard = function (data) {
    const countEl = document.getElementById('summaryHolidayCount');
    const labelEl = document.getElementById('summaryHolidayLabel');
    const badgeEl = document.getElementById('nextHolidayBadge');

    if (countEl) countEl.textContent = data.totalCount;
    if (labelEl) labelEl.textContent = `${data.year} Holidays in ${data.countryCode}`;

    if (badgeEl) {
        const next = this.findNextHoliday(data.holidays);
        if (next) {
            badgeEl.textContent = `Next: ${next.name} (${this.formatDateSimple(next.date)})`;
        } else {
            badgeEl.textContent = 'Year Complete';
        }
    }
};

HolidayWidget.findNextHoliday = function (holidays) {
    if (!holidays) return null;
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    return holidays.find(h => new Date(h.date) >= now);
};

HolidayWidget.formatDateSimple = function (dateStr) {
    const d = new Date(dateStr);
    return d.toLocaleDateString('default', { month: 'short', day: 'numeric' });
};

HolidayWidget.fetchDefaultHolidays = function () {
    // Try to sync with status bar location on load if available
    const statusLoc = document.getElementById('statusLocationText');
    if (statusLoc && statusLoc.textContent && statusLoc.textContent !== 'London') {
        // We don't have the code here easily, so we'll rely on the weather update trigger
        // but let's see if we can trigger a check
        console.log("HolidayWidget: Checking for initial sync...");
    }
};

// Initialize when ready
// Initialization handled by Dashboard.cshtml
