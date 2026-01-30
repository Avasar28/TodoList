
const TimeTracker = {
    init: function () {
        this.cacheDOM();
        this.initializeDate(); // Set default date
        this.initializeWeek(); // Set default week
        this.bindEvents();
        this.loadEntries();
        this.updateDateDisplay();
    },

    cacheDOM: function () {
        // Daily View
        this.dailyView = document.getElementById('daily-view');
        this.tableBody = document.getElementById('timeTrackerBody');
        this.totalTimeDisplay = document.getElementById('dailyTotalTime');
        this.dateDisplay = document.getElementById('trackerDateDisplay');
        this.historyDate = document.getElementById('historyDate');
        this.addBtn = document.getElementById('addEntryBtn');

        // Inputs
        this.newDesc = document.getElementById('newDesc');
        this.newStart = document.getElementById('newStart');
        this.newEnd = document.getElementById('newEnd');
        this.newDuration = document.getElementById('newDuration');
        this.totalTasksCount = document.getElementById('totalTasksCount');
        this.dailyProgressBar = document.getElementById('dailyProgressBar');
        this.endTimeError = document.getElementById('endTimeError');

        // Weekly View
        this.weeklyView = document.getElementById('weekly-view');
        this.weeklyReportBody = document.getElementById('weeklyReportBody');
        this.weekRangeDisplay = document.getElementById('weekRangeDisplay');
        this.prevWeekBtn = document.getElementById('prevWeekBtn');
        this.nextWeekBtn = document.getElementById('nextWeekBtn');

        // Weekly Stats
        this.weekTotalTime = document.getElementById('weekTotalTime');
        this.weekTotalTasks = document.getElementById('weekTotalTasks');
        this.weekAvgTime = document.getElementById('weekAvgTime');
        this.weekBestDay = document.getElementById('weekBestDay');
        this.weeklyBarChart = document.getElementById('weeklyBarChart'); // Chart Container

        // Modal Elements
        this.detailModal = document.getElementById('detailModal');
        this.modalDateTitle = document.getElementById('modalDateTitle');
        this.modalBodyContent = document.getElementById('modalBodyContent');
        this.modalEmptyState = document.getElementById('modalEmptyState');
        this.closeModalBtn = document.getElementById('closeModalBtn');

        // Tabs
        this.tabDaily = document.getElementById('tabDaily');
        this.tabWeekly = document.getElementById('tabWeekly');
    },

    initializeDate: function () {
        // Set picker to today by default using local time
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, '0');
        const day = String(now.getDate()).padStart(2, '0');
        this.historyDate.value = `${year}-${month}-${day}`;
    },

    initializeWeek: function () {
        // Set current week start (Monday)
        const now = new Date();
        const day = now.getDay();
        const diff = now.getDate() - day + (day === 0 ? -6 : 1); // Adjust when day is sunday
        this.currentWeekStart = new Date(now.setDate(diff));
        this.currentWeekStart.setHours(0, 0, 0, 0);
    },

    bindEvents: function () {
        this.addBtn.addEventListener('click', this.addEntry.bind(this));

        // Auto-calculate duration for new entry
        this.newStart.addEventListener('change', this.calculateNewDuration.bind(this));
        this.newEnd.addEventListener('change', this.calculateNewDuration.bind(this));

        // Date Picker Change
        this.historyDate.addEventListener('change', () => {
            this.updateDateDisplay();
            this.loadEntries();
        });

        // Tabs
        this.tabDaily.addEventListener('click', () => this.switchView('daily'));
        this.tabWeekly.addEventListener('click', () => this.switchView('weekly'));

        // Week Nav
        this.prevWeekBtn.addEventListener('click', () => this.changeWeek(-1));
        this.nextWeekBtn.addEventListener('click', () => this.changeWeek(1));

        // Modal Events
        this.closeModalBtn.addEventListener('click', this.closeModal.bind(this));
        this.detailModal.addEventListener('click', (e) => {
            if (e.target === this.detailModal) this.closeModal();
        });
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.detailModal.classList.contains('active')) {
                this.closeModal();
            }
        });

        // Event delegation for table actions
        this.tableBody.addEventListener('click', (e) => {
            if (e.target.closest('.btn-delete')) {
                const id = e.target.closest('.btn-delete').dataset.id;
                this.deleteEntry(id);
            }
            if (e.target.closest('.btn-edit')) {
                const id = e.target.closest('.btn-edit').dataset.id;
                this.enableEditMode(e.target.closest('tr'), id);
            }
            if (e.target.closest('.btn-save-edit')) {
                const tr = e.target.closest('tr');
                const id = e.target.closest('.btn-save-edit').dataset.id;
                this.saveEdit(tr, id);
            }
            if (e.target.closest('.btn-cancel-edit')) {
                this.loadEntries();
            }
        });

        // Weekly Report Actions (View Details)
        this.weeklyReportBody.addEventListener('click', (e) => {
            if (e.target.closest('.btn-view-details')) {
                const dateStr = e.target.closest('.btn-view-details').dataset.date;
                this.openModal(dateStr);
            }
        });

        // Recalculate duration on inline edit inputs
        this.tableBody.addEventListener('change', (e) => {
            if (e.target.classList.contains('edit-start') || e.target.classList.contains('edit-end')) {
                const tr = e.target.closest('tr');
                this.calculateEditDuration(tr);
            }
        });
    },

    switchView: function (view) {
        if (view === 'daily') {
            this.dailyView.style.display = 'block';
            this.weeklyView.style.display = 'none';
            this.tabDaily.classList.add('active');
            this.tabWeekly.classList.remove('active');
            // Hide History Date Picker in weekly view optionally? Use logic to show it only in daily
            this.historyDate.parentElement.style.display = 'block';
        } else {
            this.dailyView.style.display = 'none';
            this.weeklyView.style.display = 'block';
            this.tabDaily.classList.remove('active');
            this.tabWeekly.classList.add('active');
            this.historyDate.parentElement.style.display = 'none';
            this.loadWeeklyReport();
        }
    },

    changeWeek: function (offset) {
        this.currentWeekStart.setDate(this.currentWeekStart.getDate() + (offset * 7));
        this.loadWeeklyReport();
    },

    // Daily View Methods
    updateDateDisplay: function () {
        // Update the text display based on the picked date
        if (!this.historyDate.value) return;

        const dateParts = this.historyDate.value.split('-');
        const dateObj = new Date(dateParts[0], dateParts[1] - 1, dateParts[2]);

        const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
        this.dateDisplay.textContent = dateObj.toLocaleDateString('en-US', options);
    },

    loadEntries: function () {
        const selectedDate = this.historyDate.value;
        if (!selectedDate) return;

        fetch(`/TimeTracker/GetEntries?date=${selectedDate}`)
            .then(res => res.json())
            .then(data => {
                this.renderTable(data);
                this.calculateTotal(data);
            })
            .catch(err => console.error(err));
    },

    renderTable: function (entries) {
        this.tableBody.innerHTML = '';
        entries.forEach(entry => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
            <td data-label="Description">${this.escapeHtml(entry.description)}</td>
                <td data-label="Start">${this.formatTime(entry.startTime)}</td>
                <td data-label="End">${this.formatTime(entry.endTime)}</td>
                <td data-label="Duration">${this.formatDuration(entry.startTime, entry.endTime)}</td>
                <td class="actions-cell" data-label="Actions">
                    <button class="btn-icon btn-edit" data-id="${entry.id}" title="Edit">✏️</button>
                    <button class="btn-icon btn-delete" data-id="${entry.id}" title="Delete">🗑️</button>
                </td>
`;
            this.tableBody.appendChild(tr);
        });

        if (entries.length === 0) {
            this.tableBody.innerHTML = '<tr><td colspan="5" style="text-align:center; opacity:0.6; padding: 2rem;">No work tracked for this date.</td></tr>';
        }
    },

    // Weekly View Methods
    loadWeeklyReport: function () {
        const start = new Date(this.currentWeekStart);
        const end = new Date(start);
        end.setDate(end.getDate() + 6);

        // Format for Display
        const options = { day: 'numeric', month: 'short' };
        this.weekRangeDisplay.textContent = `${start.toLocaleDateString('en-US', options)} – ${end.toLocaleDateString('en-US', options)}`;

        // Convert to YYYY-MM-DD for API
        const startStr = this.formatDateForApi(start);
        const endStr = this.formatDateForApi(end);

        fetch(`/TimeTracker/GetWeeklyEntries?startDate=${startStr}&endDate=${endStr}`)
            .then(res => res.json())
            .then(data => {
                this.currentWeeklyEntries = data; // Store for modal
                this.renderWeeklyStats(data, start);
            })
            .catch(err => console.error(err));
    },

    renderWeeklyStats: function (entries, weekStart) {
        // Group by Date
        const daysMap = {};

        // Initialize all 7 days
        for (let i = 0; i < 7; i++) {
            const d = new Date(weekStart);
            d.setDate(d.getDate() + i);
            const dateKey = this.formatDateForApi(d);
            daysMap[dateKey] = { date: d, count: 0, minutes: 0 };
        }

        // Aggregate Data
        entries.forEach(e => {
            const dateKey = e.date.split('T')[0];
            if (daysMap[dateKey]) {
                daysMap[dateKey].count++;
                daysMap[dateKey].minutes += this.getDurationInMinutes(e.startTime, e.endTime);
            }
        });

        // Calculate Totals
        let totalWeekMinutes = 0;
        let totalWeekTasks = 0;
        let activeDays = 0;
        let maxMinutes = 0; // For Chart scaling
        let bestDay = '-';

        const rows = [];
        const chartData = []; // Prepped for renderChart

        Object.values(daysMap).forEach(dayStat => {
            totalWeekMinutes += dayStat.minutes;
            totalWeekTasks += dayStat.count;
            if (dayStat.minutes > 0) activeDays++;

            if (dayStat.minutes > maxMinutes) maxMinutes = dayStat.minutes; // Find max for scaling

            if (dayStat.minutes > 0 && dayStat.minutes === maxMinutes) {
                bestDay = dayStat.date.toLocaleDateString('en-US', { weekday: 'long' });
            }

            rows.push(`
                <tr>
                    <td data-label="Date">
                        <strong>${dayStat.date.toLocaleDateString('en-US', { weekday: 'short' })}</strong> 
                        ${dayStat.date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}
                    </td>
                    <td data-label="Total Tasks">${dayStat.count}</td>
                    <td data-label="Total Work Time">${this.formatMinutesToHHMM(dayStat.minutes)}</td>
                    <td data-label="Details" style="text-align: right;">
                        <button class="btn-icon btn-view-details" data-date="${this.formatDateForApi(dayStat.date)}" title="View Daily Details">👁️</button>
                    </td>
                </tr>
            `);

            // Push to chart data
            chartData.push({
                label: dayStat.date.toLocaleDateString('en-US', { weekday: 'short' }),
                fullDate: dayStat.date.toLocaleDateString('en-US', { weekday: 'long', month: 'short', day: 'numeric' }),
                minutes: dayStat.minutes
            });
        });

        this.weeklyReportBody.innerHTML = rows.join('');

        // Update Cards
        this.weekTotalTime.textContent = this.formatMinutesToHHMM(totalWeekMinutes);
        this.weekTotalTasks.textContent = totalWeekTasks;

        const avgMinutes = activeDays > 0 ? Math.round(totalWeekMinutes / activeDays) : 0;
        this.weekAvgTime.textContent = this.formatMinutesToHHMM(avgMinutes);
        this.weekBestDay.textContent = bestDay;

        // Render Chart
        this.renderChart(chartData, maxMinutes);
    },

    renderChart: function (data, maxMinutes) {
        // Ensure maxMinutes is at least 60 (1 hour) for scale
        const scaleMax = Math.max(maxMinutes, 60);

        this.weeklyBarChart.innerHTML = '';
        data.forEach(item => {
            const heightPercent = (item.minutes / scaleMax) * 100;
            const hourText = this.formatMinutesToHHMM(item.minutes);

            const col = document.createElement('div');
            col.className = 'chart-col';
            col.innerHTML = `
                <div class="chart-bar" style="height: ${heightPercent}%;">
                    <div class="chart-bar-tooltip">${item.fullDate}: ${hourText}</div>
                </div>
    <div class="chart-label">${item.label}</div>
`;
            this.weeklyBarChart.appendChild(col);
        });
    },

    openModal: function (dateStr) {
        // Find entries for this date
        const dailyEntries = this.currentWeeklyEntries.filter(e => e.date.startsWith(dateStr));

        // Update Modal Title
        const dateObj = new Date(dateStr);
        this.modalDateTitle.textContent = dateObj.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' });

        // Populate Table
        this.modalBodyContent.innerHTML = '';
        if (dailyEntries && dailyEntries.length > 0) {
            this.modalEmptyState.style.display = 'none';
            dailyEntries.forEach(entry => {
                const tr = document.createElement('tr');
                tr.innerHTML = `
                    <td>${this.escapeHtml(entry.description)}</td>
                    <td style="white-space:nowrap; font-size: 0.9em; opacity: 0.8;">
                        ${this.formatTime(entry.startTime)} - ${this.formatTime(entry.endTime)}
                    </td>
                    <td style="text-align: right; font-weight: 500;">
                        ${this.formatDuration(entry.startTime, entry.endTime)}
                    </td>
`;
                this.modalBodyContent.appendChild(tr);
            });
        } else {
            this.modalEmptyState.style.display = 'block';
        }

        // Show Modal
        this.detailModal.classList.add('active');
    },

    closeModal: function () {
        this.detailModal.classList.remove('active');
    },

    formatDateForApi: function (date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    },

    formatMinutesToHHMM: function (totalMinutes) {
        const h = Math.floor(totalMinutes / 60);
        const m = totalMinutes % 60;
        return `${h}h ${m}m`;
    },

    calculateTotal: function (entries) {
        let totalMinutes = 0;
        entries.forEach(entry => {
            totalMinutes += this.getDurationInMinutes(entry.startTime, entry.endTime);
        });

        const hours = Math.floor(totalMinutes / 60);
        const minutes = totalMinutes % 60;
        this.totalTimeDisplay.textContent = `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;

        // Update Smart Stats
        this.totalTasksCount.textContent = entries.length;

        // Progress Bar (Target 8 hours = 480 mins)
        const targetMins = 480;
        const percentage = Math.min((totalMinutes / targetMins) * 100, 100);
        this.dailyProgressBar.style.width = `${percentage}%`;

        // Optional Color shift near goal
        if (percentage >= 100) {
            this.dailyProgressBar.style.background = 'linear-gradient(90deg, #10b981, #059669)'; // Green success
        } else {
            this.dailyProgressBar.style.background = 'linear-gradient(90deg, #10b981, #34d399)'; // Base green
        }
    },

    addEntry: function () {
        const desc = this.newDesc.value;
        const start = this.newStart.value;
        const end = this.newEnd.value;

        if (!desc || !start || !end) {
            alert('Please fill in all fields.');
            return;
        }

        // Use selected date from picker
        const selectedDate = this.historyDate.value; // YYYY-MM-DD
        const localDate = `${selectedDate}T00:00:00`;

        const entry = {
            description: desc,
            startTime: start.length === 5 ? start + ':00' : start,
            endTime: end.length === 5 ? end + ':00' : end,
            date: localDate
        };

        fetch('/TimeTracker/Add', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(entry)
        })
            .then(res => res.json())
            .then(res => {
                if (res.success) {
                    // Clear inputs
                    this.newDesc.value = '';
                    this.newStart.value = '';
                    this.newEnd.value = '';
                    this.newDuration.value = '';
                    this.loadEntries();
                }
            });
    },

    deleteEntry: function (id) {
        if (!confirm('Are you sure you want to delete this entry?')) return;

        fetch(`/TimeTracker/Delete?id=${id}`, { method: 'POST' })
            .then(res => res.json())
            .then(res => {
                if (res.success) this.loadEntries();
            });
    },

    enableEditMode: function (tr, id) {
        const desc = tr.children[0].textContent;
        // Times need to be converted back to HH:mm for input type=time if they were formatted
        // Assuming formatTime returns HH:mm or HH:mm:ss. Input needs HH:mm
        const start = this.parseToInputTime(tr.children[1].textContent);
        const end = this.parseToInputTime(tr.children[2].textContent);

        tr.classList.add('editing-row');
        tr.innerHTML = `
            <td><input type="text" class="form-control form-control-sm edit-desc" value="${this.escapeHtml(desc)}"></td>
            <td><input type="time" class="form-control form-control-sm edit-start" value="${start}"></td>
            <td><input type="time" class="form-control form-control-sm edit-end" value="${end}"></td>
            <td class="edit-duration">...</td>
            <td class="actions-cell">
                <button class="btn-icon btn-save-edit" data-id="${id}" title="Save">💾</button>
                <button class="btn-icon btn-cancel-edit" title="Cancel">❌</button>
            </td>
`;
        this.calculateEditDuration(tr);
    },

    saveEdit: function (tr, id) {
        const desc = tr.querySelector('.edit-desc').value;
        const start = tr.querySelector('.edit-start').value;
        const end = tr.querySelector('.edit-end').value;

        // Use selected date from picker
        const selectedDate = this.historyDate.value; // YYYY-MM-DD
        const localDate = `${selectedDate}T00:00:00`;

        const entry = {
            id: id,
            description: desc,
            startTime: start.length === 5 ? start + ':00' : start,
            endTime: end.length === 5 ? end + ':00' : end,
            date: localDate
        };

        fetch('/TimeTracker/Update', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(entry)
        })
            .then(res => res.json())
            .then(res => {
                if (res.success) this.loadEntries();
            });
    },

    // calculateNewDuration, validateTimeInput etc. remain same below...

    calculateNewDuration: function () {
        const start = this.newStart.value;
        const end = this.newEnd.value;
        if (start && end) {
            this.newDuration.value = this.calcDiff(start, end);
        } else {
            this.newDuration.value = '';
        }

        // Validation check
        this.validateTimeInput(this.newStart, this.newEnd, this.endTimeError);
    },

    validateTimeInput: function (startEl, endEl, errorEl) {
        const start = startEl.value;
        const end = endEl.value;

        if (start && end) {
            const [h1, m1] = start.split(':').map(Number);
            const [h2, m2] = end.split(':').map(Number);

            // Simple comparison for strict "End > Start" on same day
            // If overnight support is desired, this validation might need adjustment
            // But usually daily trackers imply linear time.
            // "Prevent end time earlier than start time"

            const minsStart = h1 * 60 + m1;
            const minsEnd = h2 * 60 + m2;

            if (minsEnd <= minsStart) {
                endEl.classList.add('input-error');
                if (errorEl) errorEl.classList.add('show');
                this.addBtn.disabled = true;
                this.addBtn.style.opacity = '0.5';
            } else {
                endEl.classList.remove('input-error');
                if (errorEl) errorEl.classList.remove('show');
                this.addBtn.disabled = false;
                this.addBtn.style.opacity = '1';
            }
        } else {
            endEl.classList.remove('input-error');
            if (errorEl) errorEl.classList.remove('show');
            this.addBtn.disabled = false;
            this.addBtn.style.opacity = '1';
        }
    },

    calculateEditDuration: function (tr) {
        const start = tr.querySelector('.edit-start').value;
        const end = tr.querySelector('.edit-end').value;
        const display = tr.querySelector('.edit-duration');
        if (start && end) {
            display.textContent = this.calcDiff(start, end);
        } else {
            display.textContent = '--:--';
        }
    },

    calcDiff: function (start, end) {
        const [h1, m1] = start.split(':').map(Number);
        const [h2, m2] = end.split(':').map(Number);

        let diffMins = (h2 * 60 + m2) - (h1 * 60 + m1);
        if (diffMins < 0) diffMins += 24 * 60; // Handle overnight? Or specific rule? Assuming same day.

        const h = Math.floor(diffMins / 60);
        const m = diffMins % 60;
        return `${String(h).padStart(2, '0')}h ${String(m).padStart(2, '0')}m`;
    },

    getDurationInMinutes: function (start, end) {
        // start/end can be "HH:mm:ss" or "HH:mm" from server
        const cleanStart = start.substring(0, 5);
        const cleanEnd = end.substring(0, 5);

        const [h1, m1] = cleanStart.split(':').map(Number);
        const [h2, m2] = cleanEnd.split(':').map(Number);

        let diff = (h2 * 60 + m2) - (h1 * 60 + m1);
        if (diff < 0) diff += 24 * 60;
        return diff;
    },

    formatTime: function (timeStr) {
        // Server likely returns "HH:mm:ss"
        return timeStr.substring(0, 5);
    },

    parseToInputTime: function (displayTime) {
        return displayTime.trim(); // "09:00" -> "09:00"
    },

    formatDuration: function (start, end) {
        const mins = this.getDurationInMinutes(start, end);
        const h = Math.floor(mins / 60);
        const m = mins % 60;
        return `${h}h ${m}m`;
    },

    escapeHtml: function (text) {
        if (!text) return text;
        return text
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }
};

document.addEventListener('DOMContentLoaded', () => {
    TimeTracker.init();
});
