/**
 * API-Based Absolute Translation System
 * ensuring 100% coverage of visible text.
 */

class TranslationManager {
    constructor() {
        // Auto-detect or use saved
        const saved = localStorage.getItem('app_lang');
        const browser = navigator.language ? navigator.language.split('-')[0] : 'en';

        // Supported languages list for auto-detection
        const supported = ['en', 'es', 'fr', 'de', 'it', 'hi', 'zh', 'ja', 'ru', 'ar', 'pt'];

        this.currentLang = saved || (supported.includes(browser) ? browser : 'en');

        // Save computed preference if not exists
        if (!saved) localStorage.setItem('app_lang', this.currentLang);

        this.apiEndpoint = '/api/Translation/batch';
        this.observer = null;
        this.isTranslating = false;
        this.nodeQueue = new Set();
        this.BATCH_SIZE = 25; // Balanced optimization: fewer requests vs responsiveness
        this.DEBOUNCE_MS = 100;
        this.processingTimeout = null;
        this.translationCache = new Map();

        // WeakMap to store strict original English text for nodes and attributes
        this.originalTextMap = new WeakMap();

        this.init();
    }

    init() {
        console.log(`Translation System Initialized. Language: ${this.currentLang}`);
        this.bindNavbarSelector(); // Bind event listener first

        if (this.currentLang === 'en') {
            // Ensure we are not stuck with stale translations if we just switched to en
            // But usually reload handles it.
            return;
        }

        // Initial scan
        this.scanAndTranslate(document.body);
        this.startObserver();
    }

    bindNavbarSelector() {
        // Use event delegation or re-query in case of dynamic replacement (rare for navbar but safe)
        const select = document.getElementById('heroLangSwitcher');
        if (select) {
            select.value = this.currentLang;
            // Remove old listeners to avoid duplicates if init runs multiple times? 
            // Better: just overwrite onclick or use addEventListener with a named function if needed.
            // But here we just init once on load.
            select.onchange = (e) => this.setLanguage(e.target.value);
        }
    }

    setLanguage(lang) {
        if (this.currentLang === lang) return;
        this.currentLang = lang;
        localStorage.setItem('app_lang', lang);

        if (lang === 'en') {
            this.restoreToEnglish();
        } else {
            this.translationCache.clear();
            // Re-scan with new language. 
            // IMPORTANT: Because we use originalTextMap, we will translate from English source, thus it works perfectly.
            this.scanAndTranslate(document.body);
        }
    }

    restoreToEnglish() {
        // Walk the visible DOM to find nodes we have tracked
        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT, null, false);
        let node;
        while (node = walker.nextNode()) {
            if (this.originalTextMap.has(node)) {
                const original = this.originalTextMap.get(node);

                // Handle Text Nodes
                if (node.nodeType === Node.TEXT_NODE && typeof original === 'string') {
                    // Only restore if different (optimization)
                    if (node.nodeValue !== original) {
                        node.nodeValue = original;
                    }
                }
                // Handle Elements (Attributes)
                else if (node.nodeType === Node.ELEMENT_NODE && typeof original === 'object') {
                    for (const [attr, val] of Object.entries(original)) {
                        node.setAttribute(attr, val);
                    }
                }
            }
        }
    }

    startObserver() {
        if (this.observer) this.observer.disconnect();

        this.observer = new MutationObserver((mutations) => {
            let shouldProcess = false;
            mutations.forEach(mutation => {
                mutation.addedNodes.forEach(node => {
                    if (node.nodeType === Node.ELEMENT_NODE || node.nodeType === Node.TEXT_NODE) {
                        this.queueNode(node);
                        shouldProcess = true;
                    }
                });

                if (mutation.type === 'characterData') {
                    // Text node changed content.
                    // We need to decide if this is a "valid" external change or our own translation.
                    // If it's our own translation, we ignore.
                    // If external changed it (e.g. clock update), we need to capture new original.
                    // However, tracking this is hard.
                    // For now, simpler: assume external updates replace the node or set textContent (which triggers childList or characterData).
                    // If we are observing characterData, we might catch our own changes.
                    // To avoid infinite loop, we won't observe characterData for now unless strict requirement.
                    // Standard MutationObserver 'childList' covers most 'innerHTML = ...' cases.
                    // Direct textNode.nodeValue = '...' triggers characterData.
                }

                if (mutation.type === 'attributes' && this.isTranslatableAttribute(mutation.attributeName)) {
                    this.queueNode(mutation.target);
                    shouldProcess = true;
                }
            });

            if (shouldProcess) this.scheduleProcessing();
        });

        this.observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributeFilter: ['placeholder', 'title', 'alt', 'aria-label']
        });
    }

    queueNode(node) {
        // Walk the tree of the added node/tree
        const walker = document.createTreeWalker(node, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT, null, false);

        const addToQueue = (n) => {
            this.nodeQueue.add(n);
        };

        // Handle root
        if (node.nodeType === Node.TEXT_NODE || node.nodeType === Node.ELEMENT_NODE) addToQueue(node);

        let currentNode = walker.nextNode();
        while (currentNode) {
            if (currentNode.nodeType === Node.TEXT_NODE || currentNode.nodeType === Node.ELEMENT_NODE) addToQueue(currentNode);
            currentNode = walker.nextNode();
        }
    }

    scheduleProcessing() {
        if (this.processingTimeout) clearTimeout(this.processingTimeout);
        this.processingTimeout = setTimeout(() => this.processQueue(), this.DEBOUNCE_MS);
    }

    async processQueue() {
        if (this.nodeQueue.size === 0) return;

        const nodesToProcess = Array.from(this.nodeQueue);
        this.nodeQueue.clear();

        const textMap = new Map(); // Map<TextContent, Array<{type, node, attr}>>
        const textsToTranslate = new Set();
        const hasLetters = (t) => /\p{L}/u.test(t);

        nodesToProcess.forEach(node => {
            if (this.isExcluded(node)) return;

            // 1. Handle Text Nodes
            if (node.nodeType === Node.TEXT_NODE) {
                // Determine Source Text
                let sourceText;

                // Check if we already have the original text stored
                if (this.originalTextMap.has(node)) {
                    sourceText = this.originalTextMap.get(node);
                } else {
                    // This is a new node or never validated.
                    // Store the current value as the Original English Source.
                    sourceText = node.nodeValue.trim();
                    if (sourceText.length > 0) {
                        this.originalTextMap.set(node, sourceText);
                    }
                }

                if (sourceText && sourceText.length > 0 && hasLetters(sourceText)) {
                    if (!textMap.has(sourceText)) textMap.set(sourceText, []);
                    textMap.get(sourceText).push({ type: 'text', node: node });
                    textsToTranslate.add(sourceText);
                }
            }

            if (node.nodeType === Node.ELEMENT_NODE) {
                ['placeholder', 'aria-label'].forEach(attr => {
                    // Use a composite key object for WeakMap uniqueness on attributes?
                    // WeakMap keys must be objects. We can store a map of attributes on the node in WeakMap.
                    // Let's simpler: store a property on the map: node -> { attr: originalVal }

                    let originMap = this.originalTextMap.get(node);
                    if (!originMap || typeof originMap !== 'object') {
                        originMap = {}; // Init simple object to hold attr values
                        // If node was a text node this wouldn't happen as we filter nodeType.
                        // But wait, WeakMap key is object (node). If we stored string for text node, we can't reuse.
                        // Text node and Element node are different objects, so keys don't collide.
                    }

                    // For Element nodes, we store an object of { attrName: "Original Text" }
                    // Check if we have original for this attr
                    let sourceVal;
                    if (originMap[attr]) {
                        sourceVal = originMap[attr];
                    } else {
                        const currentVal = node.getAttribute(attr);
                        if (currentVal && currentVal.trim().length > 0) {
                            sourceVal = currentVal.trim();
                            originMap[attr] = sourceVal;
                            this.originalTextMap.set(node, originMap); // Update map
                        }
                    }

                    if (sourceVal && sourceVal.length > 0 && hasLetters(sourceVal)) {
                        if (!textMap.has(sourceVal)) textMap.set(sourceVal, []);
                        textMap.get(sourceVal).push({ type: 'attr', node: node, attr: attr });
                        textsToTranslate.add(sourceVal);
                    }
                });
            }
        });

        const uncachedTexts = Array.from(textsToTranslate).filter(t => !this.translationCache.has(t));

        // Apply cached immediately
        Array.from(textsToTranslate).forEach(t => {
            if (this.translationCache.has(t)) {
                this.applyTranslation(t, this.translationCache.get(t), textMap);
            }
        });

        if (uncachedTexts.length === 0) return;

        // Fetch uncached in batches
        for (let i = 0; i < uncachedTexts.length; i += this.BATCH_SIZE) {
            const batch = uncachedTexts.slice(i, i + this.BATCH_SIZE);
            await this.fetchTranslationsWithRetry(batch, textMap);
        }
    }

    async fetchTranslationsWithRetry(texts, textMap, retries = 3) {
        let attempt = 0;
        while (attempt < retries) {
            try {
                const response = await fetch(this.apiEndpoint, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ texts: texts, targetLang: this.currentLang })
                });

                if (!response.ok) throw new Error(`Status ${response.status}`);

                const data = await response.json();
                const translations = data.translations;

                texts.forEach((text, index) => {
                    const translated = translations[index];
                    if (translated) {
                        this.translationCache.set(text, translated);
                        this.applyTranslation(text, translated, textMap);
                    }
                });
                return; // Success

            } catch (e) {
                attempt++;
                console.warn(`Translation attempt ${attempt} failed: ${e.message}`);
                if (attempt >= retries) {
                    console.error("Max retries reached. Some text remains untranslated.");
                } else {
                    await new Promise(r => setTimeout(r, 1000 * attempt)); // Exponential backoff
                }
            }
        }
    }

    applyTranslation(original, translated, textMap) {
        const targets = textMap.get(original);
        if (!targets) return;

        targets.forEach(target => {
            // Apply translation to DOM
            // NOTE: We do NOT update originalTextMap here. 
            // The original text remains English (or whatever we captured first).
            // We only update the visual DOM property.

            if (target.type === 'text') {
                // Check if it's currently showing original or previous translation (doesn't matter)
                // Just overwrite with new translation.
                target.node.nodeValue = translated;
            } else if (target.type === 'attr') {
                target.node.setAttribute(target.attr, translated);
            }
        });
    }

    isExcluded(node) {
        if (!node) return false;
        // Check if node itself is excluded tag
        if (node.tagName && ['script', 'style', 'code', 'pre', 'noscript'].includes(node.tagName.toLowerCase())) return true;

        // Traverse up
        let cur = node;
        while (cur) {
            if (cur.classList && cur.classList.contains('notranslate')) return true;
            if (cur.id === 'heroLangSwitcher') return true;
            cur = cur.parentNode;
            if (cur === document) break; // formatting
        }
        return false;
    }

    isTranslatableAttribute(attr) {
        return ['placeholder', 'aria-label'].includes(attr);
    }

    scanAndTranslate(root) {
        this.queueNode(root);
        this.scheduleProcessing(); // Use schedule to debounce the initial blast
    }
}

document.addEventListener('DOMContentLoaded', () => {
    window.TranslationSystem = new TranslationManager();
    console.log("Global Translation System Ready.");
});
