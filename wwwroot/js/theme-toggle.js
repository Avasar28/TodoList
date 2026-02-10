// Immediate Theme Apply to prevent flicker
const savedTheme = localStorage.getItem('theme');
const systemPrefersDark = window.matchMedia('(prefers-color-scheme: dark)');

let initialTheme = savedTheme;
if (!initialTheme) {
    initialTheme = systemPrefersDark.matches ? 'dark' : 'light';
}
document.documentElement.setAttribute('data-theme', initialTheme);

window.toggleTheme = function () {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);

    updateThemeIcon(newTheme);

    // Animate Rotation
    const btn = document.getElementById('themeToggleBtn');
    if (btn) {
        btn.classList.add('rotate');
        setTimeout(() => btn.classList.remove('rotate'), 500);
    }
}

function updateThemeIcon(theme) {
    const btn = document.getElementById('themeToggleBtn');
    if (btn) {
        btn.innerHTML = theme === 'dark' ? '🌙' : '☀️';
        btn.setAttribute('aria-label', `Switch to ${theme === 'dark' ? 'Light' : 'Dark'} Theme`);
    }
}

// Initialize Icon on Load
document.addEventListener('DOMContentLoaded', () => {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    updateThemeIcon(currentTheme);

    // Add event listener if not added inline
    const btn = document.getElementById('themeToggleBtn');
    if (btn) {
        btn.onclick = window.toggleTheme;
    }
});

// Listen for System Changes
if (window.matchMedia) {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        if (!localStorage.getItem('theme')) {
            const newTheme = e.matches ? 'dark' : 'light';
            document.documentElement.setAttribute('data-theme', newTheme);
            updateThemeIcon(newTheme);
        }
    });
}
