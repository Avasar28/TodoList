
const TimeTracker = {
    init: function () {
        this.cacheDOM();
        this.bindEvents();
        this.loadEntries();
        this.updateDateDisplay();
    },

    cacheDOM: function () {
        this.tableBody = document.getElementById('timeTrackerBody');
        this.totalTimeDisplay = document.getElementById('dailyTotalTime');
        this.dateDisplay = document.getElementById('trackerDateDisplay');
        this.addBtn = document.getElementById('addEntryBtn');

        // Add Form Inputs
        this.newDesc = document.getElementById('newDesc');
        this.newStart = document.getElementById('newStart');
        this.newEnd = document.getElementById('newEnd');
        this.newDuration = document.getElementById('newDuration');

        // Smart Stats
        this.totalTasksCount = document.getElementById('totalTasksCount');
        this.dailyProgressBar = document.getElementById('dailyProgressBar');

        // Validation UI
        this.endTimeError = document.getElementById('endTimeError');
    },

    bindEvents: function () {
        this.addBtn.addEventListener('click', this.addEntry.bind(this));

        // Auto-calculate duration for new entry
        this.newStart.addEventListener('change', this.calculateNewDuration.bind(this));
        this.newEnd.addEventListener('change', this.calculateNewDuration.bind(this));

        // Event delegation for table actions
        this.tableBody.addEventListener('click', (e) => {
            if (e.target.closest('.btn-delete')) {
                const id = e.target.closest('.btn-delete').dataset.id;
                this.deleteEntry(id);
            }
            if (e.target.closest('.btn-edit')) {
                // Implement inline edit or modal? 
                // For simplicity given specifications, lets toggle a row to edit mode or just load into form?
                // "Actions (Save / Edit / Delete)" implies inline or replacing the row. 
                // Let's go with loading into the main form for "Edit" as a simple approach, 
                // OR better, create an inline edit experience which is cleaner.
                // Let's stick to "Delete" first, and maybe "Edit" loads it up.
                // Actually, let's try a simple inline edit if time permits, otherwise a prompt/modal.
                // Given the constraints and requested "Delete / Edit" buttons:
                const id = e.target.closest('.btn-edit').dataset.id;
                this.enableEditMode(e.target.closest('tr'), id);
            }
            if (e.target.closest('.btn-save-edit')) {
                const tr = e.target.closest('tr');
                const id = e.target.closest('.btn-save-edit').dataset.id;
                this.saveEdit(tr, id);
            }
            if (e.target.closest('.btn-cancel-edit')) {
                this.loadEntries(); // refetch to reset
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

    updateDateDisplay: function () {
        const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
        this.dateDisplay.textContent = new Date().toLocaleDateString('en-US', options);
    },

    loadEntries: function () {
        // Use local date YYYY-MM-DD
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, '0');
        const day = String(now.getDate()).padStart(2, '0');
        const today = `${year}-${month}-${day}`;

        fetch(`/TimeTracker/GetEntries?date=${today}`)
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
            this.tableBody.innerHTML = '<tr><td colspan="5" style="text-align:center; opacity:0.6; padding: 2rem;">No work tracked today. Start adding your tasks.</td></tr>';
        }
    },

    calculateTotal: function (entries) {
        let totalMinutes = 0;
        entries.forEach(entry => {
            totalMinutes += this.getDurationInMinutes(entry.startTime, entry.endTime);
        });

        const hours = Math.floor(totalMinutes / 60);
        const minutes = totalMinutes % 60;
        this.totalTimeDisplay.textContent = `${String(hours).padStart(2, '0')} : ${String(minutes).padStart(2, '0')}`;

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

        // Use local date to ensure it matches the view
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, '0');
        const day = String(now.getDate()).padStart(2, '0');
        const localDate = `${year}-${month}-${day}T00:00:00`;

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

        // Use local date to preserve day
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, '0');
        const day = String(now.getDate()).padStart(2, '0');
        const localDate = `${year}-${month}-${day}T00:00:00`;

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
        return `${String(h).padStart(2, '0')} h ${String(m).padStart(2, '0')} m`;
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
