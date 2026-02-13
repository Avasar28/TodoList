/**
 * Shared logic for Widget Detail Pages
 * Includes: Weather, Currency, Time, and general initializers.
 */

/* --- 0. Shared Helpers --- */
function focusInput(id) {
    const el = document.getElementById(id);
    if (el) el.focus();
}
window.focusInput = focusInput;

/* --- 1. Detail Panel Management --- */
function openDetails(panelId) {
    // Hide all panels first
    document.querySelectorAll('.details-panel').forEach(p => p.classList.remove('active'));

    // Show overlay
    const overlay = document.getElementById('detailsOverlay');
    if (overlay) overlay.classList.add('active');

    // Show target panel
    const panel = document.getElementById(panelId + 'Details');
    if (panel) {
        panel.classList.add('active');
        document.body.style.overflow = 'hidden'; // Prevent background scroll
    } else {
        console.warn(`Panel #${panelId}Details not found.`);
    }
}
window.openDetails = openDetails;

function closeDetails() {
    document.querySelectorAll('.details-panel').forEach(p => p.classList.remove('active'));
    const overlay = document.getElementById('detailsOverlay');
    if (overlay) overlay.classList.remove('active');
    document.body.style.overflow = ''; // Restore scroll
}
window.closeDetails = closeDetails;

/* --- 2. World Clocks (Optional, ensure compatibility) --- */
function updateWorldClocks() {
    const clocks = document.querySelectorAll('.time[data-tz]');
    const now = new Date();
    clocks.forEach(clock => {
        const tz = clock.getAttribute('data-tz');
        try {
            const timeString = now.toLocaleTimeString('en-US', { timeZone: tz, hour: '2-digit', minute: '2-digit' });
            clock.textContent = timeString;
        } catch (e) {
            console.error("Invalid timezone:", tz);
        }
    });
}
window.updateWorldClocks = updateWorldClocks;

// Auto-run clocks if present
setInterval(updateWorldClocks, 1000);
updateWorldClocks();
