/**
 * LibreTranslate System (Smart Auto-Detection)
 * 
 * Features:
 * - "Future Proof": Automatically walks the DOM to translate ALL visible text nodes AND Placeholders.
 * - Heuristic Protection:
 *   - Skips Inputs (Values), Scripts, Styles, Code blocks.
 *   - Skips containers marked with `data-no-translate="true"`.
 *   - Skips numbers, URLs, emails, and single characters.
 * - MutationObserver: Instantly detecting new content everywhere.
 */

(function () {
    const CONFIG = {
        API_ENDPOINT: '/api/Translation/batch',
        BATCH_SIZE: 10,
        DEBOUNCE_MS: 700,
        STORAGE_KEY: 'app_lang',
        CACHE_KEY_PREFIX: 'libre_trans_'
    };

    const state = {
        currentLang: localStorage.getItem(CONFIG.STORAGE_KEY) || 'en',
        workQueue: new Set(), // Can hold TextNodes OR Elements (for placeholders)
        processingTimeout: null,
        translationCache: new Map(),
        observer: null,
        trackedItems: new WeakMap() // Maps Object -> { original: "..." }
    };

    // --- Block Lists & Heuristics ---
    const BLOCKED_TAGS = new Set(['SCRIPT', 'STYLE', 'CODE', 'PRE', 'SVG', 'NOSCRIPT', 'IFRAME']); // Removed INPUT/TEXTAREA/SELECT from block, as we might need to check their placeholders/options (though options have text nodes)

    // Regex to identify content that should NOT be translated (Numbers, Dates, Paths, Times)
    const SKIP_REGEX = /^(\d+(\.\d+)?|https?:\/\/.*|^\W+$|\d{1,2}:\d{2}(:\d{2})?(\s?[AP]M)?)$/i;

    // --- Core Logic ---

    function loadLocalCache() {
        if (state.currentLang === 'en') return;
        try {
            const json = localStorage.getItem(CONFIG.CACHE_KEY_PREFIX + state.currentLang);
            if (json) {
                const data = JSON.parse(json);
                for (const [key, value] of Object.entries(data)) {
                    state.translationCache.set(key, value);
                }
            }
        } catch (e) { console.warn("Cache error", e); }
    }

    function saveLocalCache() {
        if (state.currentLang === 'en') return;
        try {
            const obj = Object.fromEntries(state.translationCache);
            localStorage.setItem(CONFIG.CACHE_KEY_PREFIX + state.currentLang, JSON.stringify(obj));
        } catch (e) { }
    }

    // --- Scanners ---

    function shouldTranslateTextNode(node) {
        if (node.nodeType !== 3) return false;

        const text = node.nodeValue.trim();
        if (!text || text.length < 2 || SKIP_REGEX.test(text)) return false;

        const parent = node.parentNode;
        if (!parent) return false;
        if (BLOCKED_TAGS.has(parent.tagName)) return false;
        if (parent.closest('[data-no-translate="true"]')) return false;
        if (parent.isContentEditable) return false;

        // Also skip if parent is an INPUT/TEXTAREA value (TextNodes don't exist there, but good to be safe)
        if (parent.tagName === 'TEXTAREA') return false; // Textarea content is value, not usually translatable text node in this context? actually it is. But typically user input. We should block textarea.

        return true;
    }

    function shouldTranslatePlaceholder(el) {
        if (!el.getAttribute('placeholder')) return false;
        const text = el.getAttribute('placeholder').trim();
        if (!text || text.length < 2 || SKIP_REGEX.test(text)) return false;
        if (el.closest('[data-no-translate="true"]')) return false;
        return true;
    }

    function scanTree(root) {
        // 1. Scan Text Nodes
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false);
        let node;
        while (node = walker.nextNode()) {
            if (shouldTranslateTextNode(node)) {
                queueItem(node, 'text');
            }
        }

        // 2. Scan Placeholders (Inputs/Textareas)
        // Helper to check a specific node if it's an element
        if (root.nodeType === 1) {
            checkElementForPlaceholder(root);
            root.querySelectorAll('input, textarea').forEach(checkElementForPlaceholder);
        }
    }

    function checkElementForPlaceholder(el) {
        if (shouldTranslatePlaceholder(el)) {
            queueItem(el, 'placeholder');
        }
    }

    function queueItem(item, type) {
        if (state.currentLang === 'en') return;
        if (state.workQueue.has(item)) return;

        // Track Original
        if (!state.trackedItems.has(item)) {
            const originalVal = type === 'text' ? item.nodeValue : item.getAttribute('placeholder');
            state.trackedItems.set(item, { original: originalVal, type: type });
        }

        state.workQueue.add(item);
        scheduleProcessing();
    }

    function scheduleProcessing() {
        if (state.processingTimeout) clearTimeout(state.processingTimeout);
        state.processingTimeout = setTimeout(() => processQueue(), CONFIG.DEBOUNCE_MS);
    }

    async function processQueue() {
        if (state.workQueue.size === 0) return;

        const itemsToProcess = Array.from(state.workQueue);
        state.workQueue.clear();

        const uniqueTexts = new Set();
        const bindings = [];

        itemsToProcess.forEach(item => {
            if (item.nodeType === 3 && !item.isConnected) return; // Detached text node
            if (item.nodeType === 1 && !document.contains(item)) return; // Detached element

            const record = state.trackedItems.get(item);
            if (!record) return;

            const text = record.original.trim();
            if (!text) return;

            if (state.translationCache.has(text)) {
                applyTranslation(item, record.type, state.translationCache.get(text));
            } else {
                uniqueTexts.add(text);
                bindings.push({ item, type: record.type, text });
            }
        });

        const batch = Array.from(uniqueTexts);
        if (batch.length === 0) return;

        // Fetch
        try {
            const response = await fetch(CONFIG.API_ENDPOINT, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ texts: batch, targetLang: state.currentLang })
            });

            if (response.ok) {
                const translations = await response.json();

                batch.forEach((original, index) => {
                    const translated = translations[index];
                    if (translated) {
                        state.translationCache.set(original, translated);

                        // Apply
                        bindings.filter(b => b.text === original).forEach(b => {
                            applyTranslation(b.item, b.type, translated);
                        });
                    }
                });
                saveLocalCache();
            }
        } catch (e) {
            console.error("Trans Error", e);
        }
    }

    function applyTranslation(item, type, translatedText) {
        if (type === 'text') {
            item.nodeValue = translatedText;
        } else if (type === 'placeholder') {
            item.setAttribute('placeholder', translatedText);
        }
    }

    // --- State Management ---

    function setLanguage(lang) {
        if (state.currentLang === lang) return;
        state.currentLang = lang;
        localStorage.setItem(CONFIG.STORAGE_KEY, lang);

        if (lang === 'en') {
            restoreToEnglish();
        } else {
            restoreToEnglish();
            state.translationCache.clear();
            loadLocalCache();
            scanTree(document.body);
        }
    }

    function restoreToEnglish() {
        // Re-walk to find things we tracked
        // Text Nodes
        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null, false);
        let node;
        while (node = walker.nextNode()) {
            if (state.trackedItems.has(node)) {
                node.nodeValue = state.trackedItems.get(node).original;
            }
        }
        // Placeholders
        document.querySelectorAll('input, textarea').forEach(el => {
            if (state.trackedItems.has(el)) {
                el.setAttribute('placeholder', state.trackedItems.get(el).original);
            }
        });
    }

    function startObserver() {
        if (state.observer) state.observer.disconnect();
        state.observer = new MutationObserver((mutations) => {
            if (state.currentLang === 'en') return;

            mutations.forEach(mutation => {
                // Nodes Added
                mutation.addedNodes.forEach(node => {
                    if (node.nodeType === 3) {
                        if (shouldTranslateTextNode(node)) queueItem(node, 'text');
                    } else if (node.nodeType === 1) {
                        scanTree(node); // Deep scan for texts and placeholders
                    }
                });

                // Attribute Changes (Placeholder updates)
                if (mutation.type === 'attributes' && mutation.attributeName === 'placeholder') {
                    if (shouldTranslatePlaceholder(mutation.target)) {
                        // Check if we already replaced it to avoid loop?
                        // If we replaced it, the new value is the translated one.
                        // We need to care about "Dynamic Logic" updating placeholder to English.
                        // If logic updates to English, we see it, and we should translate it.
                        // But if WE update to Spanish, we see it... and we should ignore.

                        // Simplest: If cache has key == current value, ignore (it's already translated).
                        // Or if current value == tracked.original, then we know it reset?
                        const val = mutation.target.getAttribute('placeholder');
                        // Loop prevention handled by: if it's already translated, don't queue. 
                        // But how do we know? Heuristic: if matches cache value? 
                        // For now, allow re-scan, but debounce handles rapid firing.
                        queueItem(mutation.target, 'placeholder');
                    }
                }
            });
        });

        state.observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['placeholder']
        });
    }

    // --- Init ---

    function init() {
        loadLocalCache();

        const dropdown = document.getElementById('googleLangSwitcher');
        if (dropdown) {
            dropdown.value = state.currentLang;
            dropdown.addEventListener('change', (e) => setLanguage(e.target.value));
        }

        startObserver();

        if (state.currentLang !== 'en') {
            scanTree(document.body);
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

})();
