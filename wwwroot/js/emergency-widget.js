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
        this.data = []; // Initialize data as empty, will be loaded via fetch
        this.init();
    }

    init() {
        this.cacheDOM();
        this.bindEvents();
        this.loadEmergencyData(); // Load data from JSON
        this.renderInitialState();

        // Listen for weather location updates to auto-detect country
        document.addEventListener('weather-location-updated', (e) => {
            if (e.detail && e.detail.country) {
                // Determine if we should auto-select
                // Only auto-select if user hasn't manually searched recently?
                // For now, let's just log it or maybe suggest it.
                // this.prefillCountry(e.detail.country);
                // Implementation decided: Just use the data for suggestions if needed,
                // but for now we wait for user input.
            }
        });
    }

    async loadEmergencyData() {
        try {
            // Fetch from internal API instead of direct file access
            const response = await fetch('/Todo/GetEmergencyNumbersJson');
            if (!response.ok) throw new Error('Failed to load emergency data');
            this.data = await response.json();
            console.log(`EmergencyWidget: Loaded ${this.data.length} countries via API.`);
        } catch (error) {
            console.error('EmergencyWidget: Error loading data', error);
            // Fallback or empty state
            this.data = [];
        }
    }

    cacheDOM() {
        this.searchInput = document.getElementById('emergencySearchInput');
        this.resultsContainer = document.getElementById('emergencyResults');
    }

    bindEvents() {
        if (this.searchInput) {
            this.searchInput.addEventListener('input', (e) => this.handleSearch(e.target.value));
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
            if (this.searchInput) {
                this.searchInput.value = match.country;
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
            item.code.toLowerCase().includes(trimmed) ||
            item.services.some(svc => svc.number.replace(/\s/g, '').includes(trimmed))
        );

        this.renderSuggestions(filtered, query);
    }

    renderSuggestions(items, query) {
        if (!this.resultsContainer) return;

        this.resultsContainer.innerHTML = '';

        if (items.length === 0) {
            this.resultsContainer.innerHTML = `
                <div class="empty-state-friendly">
                    <div class="empty-icon">🌍</div>
                    <h4>We couldn't find emergency numbers for "${query}".</h4>
                    <p>Please try searching for a nearby country or use a universal international number like <strong>112</strong> or <strong>911</strong> if applicable.</p>
                </div>
            `;
            return;
        }

        const list = document.createElement('div');
        list.className = 'emergency-suggestions-list';

        // Check if we are showing all items (initial state or empty query)
        // If so, maybe add a header? For now, just the list is fine as per "show all name".

        items.forEach(item => {
            const el = document.createElement('div');
            el.className = 'suggestion-item';
            el.onclick = () => this.renderDetail(item);

            // If searching by number, maybe highlight the matching service?
            // For now, keep it simple.

            el.innerHTML = `
                <span class="sugg-flag">${item.flag}</span>
                <span class="sugg-name">${item.country}</span>
                <span class="sugg-code">${item.code}</span>
                <span class="sugg-arrow">→</span>
            `;
            list.appendChild(el);
        });

        this.resultsContainer.appendChild(list);
    }

    renderDetail(item) {
        if (!this.resultsContainer) return;

        this.resultsContainer.innerHTML = '';

        // Simple mobile detection
        const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

        const servicesHtml = item.services.flatMap(svc => {
            // Heuristic: If service is "All Services" or "General", split into specific standard services
            if (svc.name.toLowerCase() === 'all services' || svc.name.toLowerCase() === 'general') {
                return ['Police', 'Fire', 'Ambulance'].map(type => ({
                    name: type,
                    number: svc.number,
                    type: type.toLowerCase()
                }));
            }
            return [svc];
        }).map(svc => {
            const icon = this.getIconForType(svc.type);
            const classType = svc.type ? svc.type.toLowerCase() : 'general';

            return `
                <div class="emergency-card-modern ${classType}">
                    <div class="card-accent-strip"></div>
                    <div class="card-content">
                        <div class="ec-icon-circle">
                            ${icon}
                        </div>
                        <div class="ec-info">
                            <span class="ec-service-name">${svc.name}</span>
                            <span class="ec-number">${svc.number}</span>
                        </div>
                        <div class="ec-actions">
                            <a href="tel:${svc.number.replace(/\s/g, '')}" class="btn-ec-call">
                                📞 Call
                            </a>
                            <button class="btn-ec-copy" onclick="window.emergencyWidget.copyToClipboard('${svc.number}', this)" title="Copy Number">
                                📋
                            </button>
                        </div>
                    </div>
                </div>
            `;
        }).join('');

        this.resultsContainer.innerHTML = `
            <div class="emergency-detail-view animation-fade-in">
                <div class="ed-header-modern">
                     <button class="btn-back-text" onclick="window.emergencyWidget.renderInitialState()">
                        ← Back
                    </button>
                    <div class="ed-country-info">
                        <span class="ed-flag">${item.flag}</span>
                        <h3 class="ed-country-name">${item.country}</h3>
                    </div>
                </div>
                
                <h4 class="ed-section-title">National Emergency Numbers</h4>
                <div class="emergency-grid-modern">
                    ${servicesHtml}
                </div>
                
                <div class="ed-footer-note">
                    <p>ℹ️ Always verify local emergency protocols when traveling.</p>
                </div>
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
        if (!this.resultsContainer) return;

        this.resultsContainer.innerHTML = `
            <div class="initial-search-state animation-fade-in">
                <div class="search-icon-placeholder">🆘</div>
                <p>Type a country name (e.g. "Japan", "Brazil") or an emergency number (e.g. "911", "112") to find details.</p>
                <div class="common-flags">
                    <span>🇺🇸</span><span>🇬🇧</span><span>🇮🇳</span><span>🇪🇺</span><span>🇦🇺</span>
                </div>
            </div>
        `;
    }
}

// Initialize on load
// Initialization handled by Dashboard.cshtml
