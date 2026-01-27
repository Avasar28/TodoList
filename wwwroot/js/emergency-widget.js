/**
 * Emergency Contact Widget Logic
 * Provides instant search for global emergency numbers.
 */

/*
 * Emergency Widget Logic
 * ----------------------
 * To add a new country:
 * 1. Add a new object to the `this.data` array.
 * 2. Required fields: country (string), code (ISO 2-char), flag (emoji).
 * 3. Services: Array of objects { name, number, type }.
 * 4. Supported types: 'police', 'fire', 'ambulance', 'alert' (general), 'helpline', 'disaster', 'coast'.
 */
class EmergencyWidget {
    constructor() {
        this.data = [
            {
                country: "Algeria",
                code: "DZ",
                flag: "🇩🇿",
                services: [
                    { name: "Police", number: "17", type: "police" },
                    { name: "Civil Protection", number: "14", type: "fire" }
                ]
            },
            {
                country: "Argentina",
                code: "AR",
                flag: "🇦🇷",
                services: [
                    { name: "Police", number: "101", type: "police" },
                    { name: "Fire", number: "100", type: "fire" },
                    { name: "Ambulance", number: "107", type: "ambulance" },
                    { name: "Violence Against Women", number: "144", type: "helpline" }
                ]
            },
            {
                country: "Australia",
                code: "AU",
                flag: "🇦🇺",
                services: [
                    { name: "Triple Zero (All Services)", number: "000", type: "alert" },
                    { name: "SES (Storm/Flood)", number: "132 500", type: "disaster" }
                ]
            },
            {
                country: "Brazil",
                code: "BR",
                flag: "🇧🇷",
                services: [
                    { name: "Police (Military)", number: "190", type: "police" },
                    { name: "Ambulance (SAMU)", number: "192", type: "ambulance" },
                    { name: "Fire Dept", number: "193", type: "fire" },
                    { name: "Women's Helpline", number: "180", type: "helpline" }
                ]
            },
            {
                country: "Canada",
                code: "CA",
                flag: "🇨🇦",
                services: [
                    { name: "All Services", number: "911", type: "alert" }
                ]
            },
            {
                country: "China",
                code: "CN",
                flag: "🇨🇳",
                services: [
                    { name: "Police", number: "110", type: "police" },
                    { name: "Fire", number: "119", type: "fire" },
                    { name: "Ambulance", number: "120", type: "ambulance" },
                    { name: "Traffic Police", number: "122", type: "police" }
                ]
            },
            {
                country: "France",
                code: "FR",
                flag: "🇫🇷",
                services: [
                    { name: "Europe Emergency", number: "112", type: "alert" },
                    { name: "Medical (SAMU)", number: "15", type: "ambulance" },
                    { name: "Police", number: "17", type: "police" },
                    { name: "Fire", number: "18", type: "fire" },
                    { name: "Homeless SAMU", number: "115", type: "helpline" }
                ]
            },
            {
                country: "Germany",
                code: "DE",
                flag: "🇩🇪",
                services: [
                    { name: "Police", number: "110", type: "police" },
                    { name: "Fire & Ambulance", number: "112", type: "fire" }
                ]
            },
            {
                country: "India",
                code: "IN",
                flag: "🇮🇳",
                services: [
                    { name: "National Emergency", number: "112", type: "alert" },
                    { name: "Police", number: "100", type: "police" },
                    { name: "Fire", number: "101", type: "fire" },
                    { name: "Ambulance", number: "102", type: "ambulance" },
                    { name: "Women Helpline", number: "1091", type: "helpline" },
                    { name: "Disaster Mgmt", number: "108", type: "disaster" }
                ]
            },
            {
                country: "Japan",
                code: "JP",
                flag: "🇯🇵",
                services: [
                    { name: "Police", number: "110", type: "police" },
                    { name: "Fire & Ambulance", number: "119", type: "fire" },
                    { name: "Coast Guard", number: "118", type: "coast" }
                ]
            },
            {
                country: "Mexico",
                code: "MX",
                flag: "🇲🇽",
                services: [
                    { name: "All Services", number: "911", type: "alert" }
                ]
            },
            {
                country: "New Zealand",
                code: "NZ",
                flag: "🇳🇿",
                services: [
                    { name: "All Services", number: "111", type: "alert" }
                ]
            },
            {
                country: "Russia",
                code: "RU",
                flag: "🇷🇺",
                services: [
                    { name: "General (Mobile)", number: "112", type: "alert" },
                    { name: "Fire", number: "101", type: "fire" },
                    { name: "Police", number: "102", type: "police" },
                    { name: "Ambulance", number: "103", type: "ambulance" },
                    { name: "Gas Emergency", number: "104", type: "disaster" }
                ]
            },
            {
                country: "South Africa",
                code: "ZA",
                flag: "🇿🇦",
                services: [
                    { name: "Police", number: "10111", type: "police" },
                    { name: "Ambulance/Fire", number: "10177", type: "ambulance" },
                    { name: "Cellular Emergency", number: "112", type: "alert" }
                ]
            },
            {
                country: "United Kingdom",
                code: "GB",
                flag: "🇬🇧",
                services: [
                    { name: "Emergency", number: "999", type: "alert" },
                    { name: "Medical (Non-Urgent)", number: "111", type: "helpline" }
                ]
            },
            {
                country: "United States",
                code: "US",
                flag: "🇺🇸",
                services: [
                    { name: "All Services", number: "911", type: "alert" },
                    { name: "Suicide Crisis", number: "988", type: "helpline" }
                ]
            }
        ];

        this.init();
    }

    init() {
        this.attachEventListeners();
        this.renderInitialState();

        // Listen for global weather updates to auto-detect location
        window.addEventListener('weather-location-updated', (e) => {
            if (e.detail && e.detail.country) {
                console.log("EmergencyWidget: Auto-detecting from weather", e.detail.country);
                this.autoDetect(e.detail.country);
            }
        });
    }

    attachEventListeners() {
        const searchInput = document.getElementById('emergencySearchInput');
        if (searchInput) {
            searchInput.addEventListener('input', (e) => this.handleSearch(e.target.value));
        }
    }

    autoDetect(countryName) {
        if (!countryName) return;

        // Find best match
        const match = this.data.find(item =>
            item.country.toLowerCase() === countryName.toLowerCase() ||
            countryName.toLowerCase().includes(item.country.toLowerCase()) || // e.g. "United States of America" includes "United States"
            item.country.toLowerCase().includes(countryName.toLowerCase())
        );

        if (match) {
            this.renderDetail(match);
            // Update input to reflect the auto-selection, but maybe keep it clean or show the country name
            const searchInput = document.getElementById('emergencySearchInput');
            if (searchInput) {
                searchInput.value = match.country;
            }
        }
    }

    handleSearch(query) {
        const trimmed = query.trim().toLowerCase();

        if (!trimmed) {
            this.renderInitialState();
            return;
        }

        const filtered = this.data.filter(item =>
            item.country.toLowerCase().includes(trimmed) ||
            item.code.toLowerCase().includes(trimmed)
        );

        // If user typed 'India' and it's the only result, maybe show it?
        // But the requirement says "Show suggestions while typing".
        // Let's render suggestions for now, unless explicit selection.
        this.renderSuggestions(filtered, query);
    }

    renderSuggestions(items, query) {
        const container = document.getElementById('emergencyResults');
        if (!container) return;

        container.innerHTML = '';

        if (items.length === 0) {
            container.innerHTML = `
                <div class="empty-state-friendly">
                    <div class="empty-icon">🤷‍♂️</div>
                    <h4>No data available for "${query}"</h4>
                    <p>Please try searching for another country (e.g., "France", "Japan").</p>
                </div>
            `;
            return;
        }

        const list = document.createElement('div');
        list.className = 'emergency-suggestions-list';

        items.forEach(item => {
            const el = document.createElement('div');
            el.className = 'suggestion-item';
            el.onclick = () => this.renderDetail(item);
            el.innerHTML = `
                <span class="sugg-flag">${item.flag}</span>
                <span class="sugg-name">${item.country}</span>
                <span class="sugg-code">${item.code}</span>
                <span class="sugg-arrow">→</span>
            `;
            list.appendChild(el);
        });

        container.appendChild(list);
    }

    renderDetail(item) {
        const container = document.getElementById('emergencyResults');
        if (!container) return;

        container.innerHTML = '';

        // Simple mobile detection
        const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

        const servicesHtml = item.services.map(svc => {
            const icon = this.getIconForType(svc.type);
            const classType = svc.type || 'general';

            // Logic:
            // Mobile: Call Button (Primary) + Copy Button (Icon)
            // Desktop: Number Text (Primary) + Copy Button (Primary Action)

            let actionRow = '';

            if (isMobile) {
                // Mobile Layout: Big Call Button
                actionRow = `
                    <div class="action-row mobile">
                        <a href="tel:${svc.number.replace(/\s/g, '')}" class="btn-action call" aria-label="Call ${svc.name}">
                            📞 Call ${svc.number}
                        </a>
                        <button class="btn-action copy-icon-only" onclick="window.emergencyWidget.copyToClipboard('${svc.number}', this)" aria-label="Copy number">
                            📋
                        </button>
                    </div>
                `;
            } else {
                // Desktop Layout: Text + Copy Button
                actionRow = `
                    <div class="action-row desktop">
                         <span class="number-text-large">${svc.number}</span>
                         <button class="btn-action copy" onclick="window.emergencyWidget.copyToClipboard('${svc.number}', this)" aria-label="Copy number">
                            📋 Copy
                        </button>
                    </div>
                `;
            }

            return `
                <div class="emergency-number-box ${classType}">
                    <span class="service-icon" aria-hidden="true">${icon}</span>
                    <span class="service-name">${svc.name}</span>
                    ${actionRow}
                </div>
            `;
        }).join('');

        container.innerHTML = `
            <div class="result-header-large animation-fade-in">
                <button class="back-link" onclick="window.emergencyWidget.handleSearch(document.getElementById('emergencySearchInput').value || '')" aria-label="Back to search results">
                    ← Back
                </button>
                <div class="rh-main">
                    <span class="rh-flag" aria-hidden="true">${item.flag}</span>
                    <div class="rh-text">
                        <h3>${item.country}</h3>
                        <span class="service-count-badge">${item.services.length} Emergency Services Available</span>
                    </div>
                </div>
            </div>
            <div class="emergency-numbers-grid animation-slide-up">
                ${servicesHtml}
            </div>
        `;
    }

    async copyToClipboard(text, btn) {
        try {
            await navigator.clipboard.writeText(text);
            const originalIcon = btn.innerHTML;
            btn.innerHTML = '✅';
            btn.classList.add('copied');
            setTimeout(() => {
                btn.innerHTML = originalIcon;
                btn.classList.remove('copied');
            }, 1500);
        } catch (err) {
            console.error('Failed to copy:', err);
        }
        // Stop propagation so we don't trigger card clicks if any
        if (event) event.stopPropagation();
    }

    getIconForType(type) {
        const map = {
            'police': '👮',
            'fire': '🚒',
            'ambulance': '🚑',
            'alert': '🚨',
            'helpline': '📞',
            'disaster': '🌪️',
            'coast': '🚤',
            'general': '🆘'
        };
        return map[type] || '🆘';
    }

    renderInitialState() {
        const container = document.getElementById('emergencyResults');
        if (!container) return;

        container.innerHTML = `
            <div class="initial-search-state">
                <div class="search-icon-placeholder">🆘</div>
                <p>Type a country name to instantly find emergency numbers.</p>
                <div class="common-flags">
                    <span>🇺🇸</span><span>🇬🇧</span><span>🇮🇳</span><span>🇪🇺</span><span>🇦🇺</span>
                </div>
            </div>
        `;
    }
}

// Initialize on load
document.addEventListener('DOMContentLoaded', () => {
    window.emergencyWidget = new EmergencyWidget();
});
