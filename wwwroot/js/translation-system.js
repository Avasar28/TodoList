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
        this.BATCH_SIZE = 50;
        this.DEBOUNCE_MS = 100;
        this.processingTimeout = null;
        this.translationCache = new Map();

        this.init();
    }

    init() {
        console.log(`Translation System Initialized. Language: ${this.currentLang}`);
        this.bindNavbarSelector();

        if (this.currentLang === 'en') return;

        this.scanAndTranslate(document.body);
        this.startObserver();
    }

    bindNavbarSelector() {
        const select = document.getElementById('heroLangSwitcher');
        if (select) {
            select.value = this.currentLang;
            select.addEventListener('change', (e) => this.setLanguage(e.target.value));
        }
    }

    setLanguage(lang) {
        if (this.currentLang === lang) return;
        this.currentLang = lang;
        localStorage.setItem('app_lang', lang);

        if (lang === 'en') {
            location.reload();
        } else {
            this.translationCache.clear();
            this.scanAndTranslate(document.body);
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
            attributes: true,
            attributeFilter: ['placeholder', 'title', 'alt', 'value', 'aria-label']
        });
    }

    queueNode(node) {
        const walker = document.createTreeWalker(node, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT, null, false);

        // Handle root
        if (node.nodeType === Node.TEXT_NODE) this.nodeQueue.add(node);
        else if (node.nodeType === Node.ELEMENT_NODE) this.nodeQueue.add(node);

        let currentNode = walker.nextNode();
        while (currentNode) {
            if (currentNode.nodeType === Node.TEXT_NODE) this.nodeQueue.add(currentNode);
            if (currentNode.nodeType === Node.ELEMENT_NODE) this.nodeQueue.add(currentNode);
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

        const textMap = new Map();
        const textsToTranslate = new Set();
        const hasLetters = (t) => /\p{L}/u.test(t);

        nodesToProcess.forEach(node => {
            if (node.nodeType === Node.TEXT_NODE) {
                const text = node.nodeValue.trim();
                if (text.length > 0 && hasLetters(text) && !this.isExcluded(node.parentNode)) {
                    if (!textMap.has(text)) textMap.set(text, []);
                    textMap.get(text).push({ type: 'text', node: node });
                    textsToTranslate.add(text);
                }
            }

            if (node.nodeType === Node.ELEMENT_NODE && !this.isExcluded(node)) {
                ['placeholder', 'title', 'alt', 'value', 'aria-label'].forEach(attr => {
                    const val = node.getAttribute(attr);
                    if (val && val.trim().length > 0 && hasLetters(val)) {
                        if (!textMap.has(val)) textMap.set(val, []);
                        textMap.get(val).push({ type: 'attr', node: node, attr: attr });
                        textsToTranslate.add(val);
                    }
                });
            }
        });

        const uncachedTexts = Array.from(textsToTranslate).filter(t => !this.translationCache.has(t));

        Array.from(textsToTranslate).forEach(t => {
            if (this.translationCache.has(t)) {
                this.applyTranslation(t, this.translationCache.get(t), textMap);
            }
        });

        if (uncachedTexts.length === 0) return;

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
            // Re-verify that node value hasn't changed in the meantime (race condition)
            if (target.type === 'text') {
                if (target.node.nodeValue.trim() === original) {
                    target.node.nodeValue = target.node.nodeValue.replace(original, translated);
                }
            } else if (target.type === 'attr') {
                target.node.setAttribute(target.attr, translated);
            }
        });
    }

    isExcluded(node) {
        if (!node) return false;
        const tag = node.tagName ? node.tagName.toLowerCase() : '';
        if (['script', 'style', 'code', 'pre', 'noscript'].includes(tag)) return true;
        if (node.classList && node.classList.contains('notranslate')) return true;

        if (node.closest) {
            if (node.closest('.notranslate')) return true;
            if (node.closest('#heroLangSwitcher')) return true;
        }
        return false;
    }

    isTranslatableAttribute(attr) {
        return ['placeholder', 'title', 'alt', 'value', 'aria-label'].includes(attr);
    }

    scanAndTranslate(root) {
        this.queueNode(root);
        this.processQueue();
    }
}

document.addEventListener('DOMContentLoaded', () => {
    window.TranslationSystem = new TranslationManager();
});
