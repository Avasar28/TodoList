/**
 * Language Translator Widget V3
 * Handles:
 * - Custom Searchable Dropdowns
 * - Theme-aware updates
 * - Auto-detect and Translation Logic
 * - Copy/Speak Interaction
 */
const TranslatorWidget = (function () {
    console.log("TranslatorWidget: Script loaded and IIFE started.");
    const CONFIG = {
        API_ENDPOINT: '/api/Translation/batch',
        DEBOUNCE_MS: 1000
    };

    const state = {
        sourceLang: 'auto',
        targetLang: 'en',
        typingTimer: null,
        isTranslating: false,
        hasRetried: false,
        initialized: false // Phase 3: Prevent double-init
    };

    // Full Language List
    const languages = [
        { code: 'auto', name: 'Auto Detect', native: 'Detect Language' },
        { code: 'af', name: 'Afrikaans', native: 'Afrikaans' },
        { code: 'sq', name: 'Albanian', native: 'Shqip' },
        { code: 'am', name: 'Amharic', native: 'አማርኛ' },
        { code: 'ar', name: 'Arabic', native: 'العربية' },
        { code: 'hy', name: 'Armenian', native: 'Հայերեն' },
        { code: 'az', name: 'Azerbaijani', native: 'Azərbaycan' },
        { code: 'eu', name: 'Basque', native: 'Euskara' },
        { code: 'be', name: 'Belarusian', native: 'Беларуская' },
        { code: 'bn', name: 'Bengali', native: 'বাংলা' },
        { code: 'bs', name: 'Bosnian', native: 'Bosanski' },
        { code: 'bg', name: 'Bulgarian', native: 'Български' },
        { code: 'ca', name: 'Catalan', native: 'Català' },
        { code: 'ceb', name: 'Cebuano', native: 'Binisaya' },
        { code: 'zh', name: 'Chinese (Simplified)', native: '简体中文' },
        { code: 'zh-TW', name: 'Chinese (Traditional)', native: '繁體中文' },
        { code: 'co', name: 'Corsican', native: 'Corsu' },
        { code: 'hr', name: 'Croatian', native: 'Hrvatski' },
        { code: 'cs', name: 'Czech', native: 'Čeština' },
        { code: 'da', name: 'Danish', native: 'Dansk' },
        { code: 'nl', name: 'Dutch', native: 'Nederlands' },
        { code: 'en', name: 'English', native: 'English' },
        { code: 'eo', name: 'Esperanto', native: 'Esperanto' },
        { code: 'et', name: 'Estonian', native: 'Eesti' },
        { code: 'fi', name: 'Finnish', native: 'Suomi' },
        { code: 'fr', name: 'French', native: 'Français' },
        { code: 'fy', name: 'Frisian', native: 'Frysk' },
        { code: 'gl', name: 'Galician', native: 'Galego' },
        { code: 'ka', name: 'Georgian', native: 'ქართული' },
        { code: 'de', name: 'German', native: 'Deutsch' },
        { code: 'el', name: 'Greek', native: 'Ελληνικά' },
        { code: 'gu', name: 'Gujarati', native: 'ગુજરાતી' },
        { code: 'ht', name: 'Haitian Creole', native: 'Kreyòl Ayisyen' },
        { code: 'ha', name: 'Hausa', native: 'Hausa' },
        { code: 'haw', name: 'Hawaiian', native: 'Ōlelo Hawaiʻi' },
        { code: 'he', name: 'Hebrew', native: 'עברית' },
        { code: 'hi', name: 'Hindi', native: 'हिन्दी' },
        { code: 'hmn', name: 'Hmong', native: 'Hmoob' },
        { code: 'hu', name: 'Hungarian', native: 'Magyar' },
        { code: 'is', name: 'Icelandic', native: 'Íslenska' },
        { code: 'ig', name: 'Igbo', native: 'Igbo' },
        { code: 'id', name: 'Indonesian', native: 'Bahasa Indonesia' },
        { code: 'ga', name: 'Irish', native: 'Gaeilge' },
        { code: 'it', name: 'Italian', native: 'Italiano' },
        { code: 'ja', name: 'Japanese', native: '日本語' },
        { code: 'jv', name: 'Javanese', native: 'Basa Jawa' },
        { code: 'kn', name: 'Kannada', native: 'ಕನ್ನಡ' },
        { code: 'kk', name: 'Kazakh', native: 'Қазақ тілі' },
        { code: 'km', name: 'Khmer', native: 'ខ្មែរ' },
        { code: 'rw', name: 'Kinyarwanda', native: 'Ikinyarwanda' },
        { code: 'ko', name: 'Korean', native: '한국어' },
        { code: 'ku', name: 'Kurdish', native: 'Kurdî' },
        { code: 'ky', name: 'Kyrgyz', native: 'Кыргызча' },
        { code: 'lo', name: 'Lao', native: 'ລາວ' },
        { code: 'la', name: 'Latin', native: 'Latina' },
        { code: 'lv', name: 'Latvian', native: 'Latviešu' },
        { code: 'lt', name: 'Lithuanian', native: 'Lietuvių' },
        { code: 'lb', name: 'Luxembourgish', native: 'Lëtzebuergesch' },
        { code: 'mk', name: 'Macedonian', native: 'Македонски' },
        { code: 'mg', name: 'Malagasy', native: 'Malagasy' },
        { code: 'ms', name: 'Malay', native: 'Bahasa Melayu' },
        { code: 'ml', name: 'Malayalam', native: 'മലയാളം' },
        { code: 'mt', name: 'Maltese', native: 'Malti' },
        { code: 'mi', name: 'Maori', native: 'Māori' },
        { code: 'mr', name: 'Marathi', native: 'मराठी' },
        { code: 'mn', name: 'Mongolian', native: 'Монгол' },
        { code: 'my', name: 'Myanmar (Burmese)', native: 'မြန်မာစာ' },
        { code: 'ne', name: 'Nepali', native: 'नेपाली' },
        { code: 'no', name: 'Norwegian', native: 'Norsk' },
        { code: 'ny', name: 'Nyanja (Chichewa)', native: 'Chichewa' },
        { code: 'or', name: 'Odia (Oriya)', native: 'ଓଡ଼ିଆ' },
        { code: 'ps', name: 'Pashto', native: 'پښتو' },
        { code: 'fa', name: 'Persian', native: 'فارسی' },
        { code: 'pl', name: 'Polish', native: 'Polski' },
        { code: 'pt', name: 'Portuguese', native: 'Português' },
        { code: 'pa', name: 'Punjabi', native: 'ਪੰਜਾਬੀ' },
        { code: 'ro', name: 'Romanian', native: 'Română' },
        { code: 'ru', name: 'Russian', native: 'Русский' },
        { code: 'sm', name: 'Samoan', native: 'Gagana Samoa' },
        { code: 'gd', name: 'Scots Gaelic', native: 'Gàidhlig' },
        { code: 'sr', name: 'Serbian', native: 'Српски' },
        { code: 'st', name: 'Sesotho', native: 'Sesotho' },
        { code: 'sn', name: 'Shona', native: 'ChiShona' },
        { code: 'sd', name: 'Sindhi', native: 'سنڌي' },
        { code: 'si', name: 'Sinhala', native: 'සිංහල' },
        { code: 'sk', name: 'Slovak', native: 'Slovenčina' },
        { code: 'sl', name: 'Slovenian', native: 'Slovenščina' },
        { code: 'so', name: 'Somali', native: 'Soomaali' },
        { code: 'es', name: 'Spanish', native: 'Español' },
        { code: 'su', name: 'Sundanese', native: 'Basa Sunda' },
        { code: 'sw', name: 'Swahili', native: 'Kiswahili' },
        { code: 'sv', name: 'Swedish', native: 'Svenska' },
        { code: 'tl', name: 'Tagalog (Filipino)', native: 'Filipino' },
        { code: 'tg', name: 'Tajik', native: 'Тоҷикӣ' },
        { code: 'ta', name: 'Tamil', native: 'தமிழ்' },
        { code: 'tt', name: 'Tatar', native: 'Татар' },
        { code: 'te', name: 'Telugu', native: 'తెలుగు' },
        { code: 'th', name: 'Thai', native: 'ไทย' },
        { code: 'tr', name: 'Turkish', native: 'Türkçe' },
        { code: 'tk', name: 'Turkmen', native: 'Türkmen' },
        { code: 'uk', name: 'Ukrainian', native: 'Українська' },
        { code: 'ur', name: 'Urdu', native: 'اردو' },
        { code: 'ug', name: 'Uyghur', native: 'ئۇيغۇرچە' },
        { code: 'uz', name: 'Uzbek', native: 'Oʻzbek' },
        { code: 'vi', name: 'Vietnamese', native: 'Tiếng Việt' },
        { code: 'cy', name: 'Welsh', native: 'Cymraeg' },
        { code: 'xh', name: 'Xhosa', native: 'isiXhosa' },
        { code: 'yi', name: 'Yiddish', native: 'ייִדיש' },
        { code: 'yo', name: 'Yoruba', native: 'Yorùbá' },
        { code: 'zu', name: 'Zulu', native: 'isiZulu' }
    ];

    // --- DOM Elements ---
    let sourceInput, targetInput, swapBtn, statusIndicator;
    let detectedSpan, actionBtn, copyBtn, speakBtn, micBtn;

    // Custom Dropdown Elements
    let sourceWrapper, sourceSearch, sourceOptionsList, sourceLabel, sourceHidden;
    let targetWrapper, targetSearch, targetOptionsList, targetLabel, targetHidden;

    function init() {
        if (state.initialized) {
            console.warn("TranslatorWidget: Already initialized. Skipping duplicate call.");
            return;
        }
        console.log("TranslatorWidget: Initializing...");

        sourceInput = document.getElementById('transSourceText');
        targetInput = document.getElementById('transTargetText');
        swapBtn = document.getElementById('transSwapBtn');
        statusIndicator = document.getElementById('transStatus');
        detectedSpan = document.getElementById('transDetectedLang');
        actionBtn = document.getElementById('transActionBtn');
        copyBtn = document.getElementById('transCopyBtn');
        speakBtn = document.getElementById('transSpeakBtn');
        micBtn = document.getElementById('transMicBtn');

        // Dropdown Logic
        initDropdown('source', languages);
        initDropdown('target', languages.filter(l => l.code !== 'auto'));

        setupEventListeners();
        setupSpeechRecognition();

        state.initialized = true;
        console.log("TranslatorWidget: Initialization complete.");
        checkPermissions();
    }

    // --- DROPBOWN LOGIC ---

    function initFallbackSelect(type, langList) {
        const select = document.getElementById(`${type}LangFallback`);
        if (!select) return;

        // Clear existing (except first if needed, but we'll rebuild)
        select.innerHTML = '';

        langList.forEach(l => {
            const opt = document.createElement('option');
            opt.value = l.code;
            opt.textContent = `${l.name} (${l.native})`;
            if (type === 'target' && l.code === 'en') opt.selected = true;
            if (type === 'source' && l.code === 'auto') opt.selected = true;
            select.appendChild(opt);
        });

        select.addEventListener('change', (e) => {
            const val = e.target.value;
            // Update State
            if (type === 'source') {
                state.sourceLang = val;
                triggerTranslation();
            } else {
                state.targetLang = val;
                triggerTranslation();
            }
            // Also update custom UI hidden input
            document.getElementById(`trans${type.charAt(0).toUpperCase() + type.slice(1)}Lang`).value = val;
        });
    }

    function initDropdown(type, langList) {
        const wrapper = document.getElementById(`${type}LangWrapper`);
        const searchInput = document.getElementById(`${type}Search`);
        // Use NEW ID to ensure fresh binding
        const optionsList = document.getElementById(`${type}OptionsListNew`);

        console.log(`Init Dropdown [${type}]: Wrapper=${!!wrapper}, List=${!!optionsList}, Items=${langList.length}`);

        if (!wrapper) {
            console.error(`Link Error: Wrapper for ${type} not found!`);
            return;
        }

        // Store references
        if (type === 'source') {
            sourceWrapper = wrapper;
            sourceSearch = searchInput;
            sourceOptionsList = optionsList;
            sourceLabel = document.getElementById(`${type}LangLabel`);
            sourceHidden = document.getElementById(`trans${type.charAt(0).toUpperCase() + type.slice(1)}Lang`);
        } else {
            targetWrapper = wrapper;
            targetSearch = searchInput;
            targetOptionsList = optionsList;
            targetLabel = document.getElementById(`${type}LangLabel`);
            targetHidden = document.getElementById(`trans${type.charAt(0).toUpperCase() + type.slice(1)}Lang`);
        }

        // Render Options
        renderOptions(optionsList, langList, document.getElementById(`trans${type.charAt(0).toUpperCase() + type.slice(1)}Lang`).value);

        // Event: Toggle
        const trigger = wrapper.querySelector('.custom-select-trigger');
        const customOptions = wrapper.querySelector('.custom-options');

        if (trigger) {
            trigger.addEventListener('click', (e) => {
                console.log(`Dropdown Clicked: ${type}`);

                const wasOpen = wrapper.classList.contains('open');
                closeAllDropdowns(wrapper); // Close others

                if (!wasOpen) {
                    wrapper.classList.add('open');
                    // Force Styles
                    if (customOptions) {
                        customOptions.style.opacity = '1';
                        customOptions.style.visibility = 'visible';
                        customOptions.style.transform = 'translateY(0)';
                        customOptions.style.display = 'block';
                    }

                    searchInput.focus();
                    searchInput.value = '';
                    filterOptions(optionsList, langList, ''); // Reset filter
                } else {
                    wrapper.classList.remove('open');
                    // Reset Styles
                    if (customOptions) {
                        customOptions.style.opacity = '';
                        customOptions.style.visibility = '';
                        customOptions.style.transform = '';
                        customOptions.style.display = '';
                    }
                }
            });
        } else {
            console.error(`Link Error: Trigger for ${type} not found!`);
        }

        // Event: Search
        searchInput.addEventListener('input', (e) => {
            filterOptions(optionsList, langList, e.target.value);
        });

        // Event: Option Click (Delegated)
        optionsList.addEventListener('click', (e) => {
            const option = e.target.closest('.custom-option');
            if (!option) return;

            console.log("Option Clicked:", option.dataset.name);

            const value = option.dataset.value;
            const name = option.dataset.name;

            // Update UI
            // FIX: Re-select hidden input as local variable was removed
            const hiddenInput = document.getElementById(`trans${type.charAt(0).toUpperCase() + type.slice(1)}Lang`);
            if (hiddenInput) hiddenInput.value = value;

            // Also update the FALLBACK select if it exists, to keep in sync
            const fallbackSelect = document.getElementById(`${type}LangFallback`);
            if (fallbackSelect) fallbackSelect.value = value;

            // Update Label
            const labelEl = type === 'source' ? document.getElementById('sourceLangLabel') : document.getElementById('targetLangLabel');
            if (labelEl) labelEl.textContent = name;

            // Close and Reset Styles
            wrapper.classList.remove('open');
            closeAllDropdowns(wrapper); // Ensure cleanup

            // Force reset of THIS wrapper's custom options specifically just in case
            if (customOptions) {
                customOptions.style.display = '';
                customOptions.style.visibility = '';
                customOptions.style.opacity = '';
            }

            // Update selected class
            Array.from(optionsList.children).forEach(c => c.classList.remove('selected'));
            option.classList.add('selected');

            // Trigger State Update
            if (type === 'source') {
                state.sourceLang = value;
                detectedSpan.textContent = '';
                triggerTranslation();
            } else {
                state.targetLang = value;
                triggerTranslation();
            }
        });
    }

    function renderOptions(container, list, selectedValue) {
        if (!container) {
            console.error("Render Error: Container is null");
            return;
        }
        const html = list.map(l => `
            <div class="custom-option ${l.code === selectedValue ? 'selected' : ''}" 
                 data-value="${l.code}" 
                 data-name="${l.name}">
                 ${l.name} <span style="opacity:0.5; font-size:0.8em; margin-left:4px;">${l.native}</span>
            </div>
        `).join('');
        container.innerHTML = html;
        console.log(`Rendered Options: Length=${html.length}, First 50 chars=${html.substring(0, 50)}...`);
    }

    function filterOptions(container, list, query) {
        const q = query.toLowerCase();
        const filtered = list.filter(l =>
            l.name.toLowerCase().includes(q) ||
            l.native.toLowerCase().includes(q) ||
            l.code.toLowerCase().includes(q)
        );

        if (filtered.length === 0) {
            container.innerHTML = '<div style="padding:10px; color:rgba(255,255,255,0.5); font-style:italic;">No results found</div>';
        } else {
            // Re-render (we lose selection state visual temporarily on search, but that's fine)
            renderOptions(container, filtered, (container === sourceOptionsList ? state.sourceLang : state.targetLang));
        }
    }

    function closeAllDropdowns(except = null) {
        document.querySelectorAll('.custom-select-wrapper').forEach(w => {
            if (w !== except) {
                w.classList.remove('open');
                const customOptions = w.querySelector('.custom-options');
                if (customOptions) {
                    customOptions.style.opacity = '';
                    customOptions.style.visibility = '';
                    customOptions.style.transform = '';
                    customOptions.style.display = '';
                }
            }
        });
    }

    function setupEventListeners() {
        // Close dropdowns on outside click
        document.addEventListener('click', (e) => {
            if (!e.target.closest('.custom-select-wrapper')) {
                closeAllDropdowns();
            }
        });

        // Swap Logic
        swapBtn.addEventListener('click', swapLanguages);

        // Input Logic
        sourceInput.addEventListener('input', () => {
            clearTimeout(state.typingTimer);
            if (sourceInput.value.trim() === '') {
                targetInput.value = '';
                detectedSpan.textContent = '';
                return;
            }
            state.typingTimer = setTimeout(triggerTranslation, CONFIG.DEBOUNCE_MS);
        });

        actionBtn.addEventListener('click', triggerTranslation);

        // Tools
        copyBtn.addEventListener('click', () => {
            if (targetInput.value) {
                navigator.clipboard.writeText(targetInput.value);
                const originalText = copyBtn.textContent;
                copyBtn.textContent = '✅';
                setTimeout(() => copyBtn.textContent = originalText, 1500);
            }
        });

        speakBtn.addEventListener('click', () => {
            const text = targetInput.value;
            if (!text) return;

            console.log(`Speaking: "${text}" in language: ${state.targetLang}`);

            // Cancel any current speech
            window.speechSynthesis.cancel();

            const utterance = new SpeechSynthesisUtterance(text);
            utterance.lang = state.targetLang;

            // Voice Selection Logic
            const voices = window.speechSynthesis.getVoices();
            console.log("Available voices:", voices.length);

            // Try 1: Exact Match (e.g. 'gu-IN' === 'gu-IN')
            let selectedVoice = voices.find(v => v.lang === state.targetLang);

            // Try 2: Prefix Match (e.g. 'gu' matches 'gu-IN')
            if (!selectedVoice) {
                selectedVoice = voices.find(v => v.lang.startsWith(state.targetLang));
            }

            // Try 3: Name Match (Intelligent Fallback)
            if (!selectedVoice) {
                const langObj = languages.find(l => l.code === state.targetLang);
                if (langObj) {
                    // Check if voice name contains "Gujarati" or "ગુજરાતી"
                    selectedVoice = voices.find(v =>
                        v.name.toLowerCase().includes(langObj.name.toLowerCase()) ||
                        v.name.toLowerCase().includes(langObj.native.toLowerCase())
                    );
                }
            }

            if (selectedVoice) {
                console.log("Selected Voice:", selectedVoice.name, selectedVoice.lang);
                utterance.voice = selectedVoice;
                utterance.onerror = (e) => console.error("Speech Synthesis Error:", e);
                window.speechSynthesis.speak(utterance);
            } else {
                console.warn("No local voice found for:", state.targetLang, ". Switching to Online Fallback.");

                // Fallback: Use Google TTS API
                try {
                    const audio = new Audio();
                    // encoding text/lang for URL
                    const encodedText = encodeURIComponent(text);
                    const lang = state.targetLang;
                    // Use a client ID that works (tw-ob is common for these hacks)
                    audio.src = `https://translate.google.com/translate_tts?ie=UTF-8&q=${encodedText}&tl=${lang}&client=tw-ob`;
                    audio.play().catch(e => {
                        console.error("Online TTS Playback Failed:", e);
                        alert("Audio not available for this language.");
                    });
                } catch (e) {
                    console.error("Google TTS Fallback Error:", e);
                }
            }
        });
    }

    function updateDropdownSelection(type, value) {
        const list = type === 'source' ? languages : languages.filter(l => l.code !== 'auto');
        const hidden = type === 'source' ? sourceHidden : targetHidden;
        const label = type === 'source' ? sourceLabel : targetLabel;
        const optionsList = type === 'source' ? sourceOptionsList : targetOptionsList;

        const lang = list.find(l => l.code === value);
        if (lang) {
            hidden.value = value;
            label.textContent = lang.name;
            // Re-render to show selected
            renderOptions(optionsList, list, value);
        }
    }

    function swapLanguages() {
        const currentSource = state.sourceLang;
        const currentTarget = state.targetLang;

        if (currentSource === 'auto') {
            // Cannot simple swap auto. 
            // Logic: Set Source = Target, Target = English (default)
            state.sourceLang = currentTarget;
            state.targetLang = 'en';
        } else {
            state.sourceLang = currentTarget;
            state.targetLang = currentSource;
        }

        // Update UI Dropdowns
        updateDropdownSelection('source', state.sourceLang);
        updateDropdownSelection('target', state.targetLang);

        // Swap Text
        const sourceText = sourceInput.value;
        const targetText = targetInput.value;
        sourceInput.value = targetText;
        targetInput.value = sourceText;

        detectedSpan.textContent = '';
        triggerTranslation();
    }

    async function triggerTranslation() {
        const text = sourceInput.value.trim();
        if (!text) return;

        if (state.sourceLang === state.targetLang) {
            targetInput.value = text;
            return;
        }

        setLoading(true);

        try {
            const response = await fetch(CONFIG.API_ENDPOINT, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    texts: [text],
                    targetLang: state.targetLang,
                    sourceLang: state.sourceLang
                })
            });

            if (response.ok) {
                const results = await response.json();
                if (results && results.length > 0) {
                    const result = results[0];
                    targetInput.value = result.translatedText;

                    if (state.sourceLang === 'auto' && result.detectedLanguage) {
                        const lang = languages.find(l => l.code === result.detectedLanguage);
                        const langName = lang ? lang.name : result.detectedLanguage;
                        detectedSpan.textContent = `Detected: ${langName}`;
                    } else {
                        detectedSpan.textContent = '';
                    }
                }
            } else {
                targetInput.value = "Translation Error";
            }
        } catch (error) {
            console.error(error);
            targetInput.value = "Network Error";
        } finally {
            setLoading(false);
        }
    }

    function setLoading(isLoading) {
        state.isTranslating = isLoading;
        if (statusIndicator) statusIndicator.style.opacity = isLoading ? '1' : '0';
        if (targetInput) targetInput.style.opacity = isLoading ? '0.7' : '1';
        if (actionBtn) actionBtn.textContent = isLoading ? '...' : 'Translate';
    }

    // --- Voice Input Logic ---
    let recognition;
    let isRecording = false;

    function setupSpeechRecognition() {
        console.log("Speech Lifecycle: Binding Main Microphone Listener...");
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

        if (!micBtn) {
            console.warn("Speech Lifecycle: Main Mic button 'transMicBtn' NOT FOUND in DOM");
            return;
        }

        if (!SpeechRecognition) {
            console.warn("Speech Lifecycle: Web Speech API not supported. Hiding Mic Button.");
            micBtn.style.display = 'none';
            return;
        }

        // Use a flag to avoid multiple listeners if somehow init is called incorrectly
        if (micBtn._hasListener) return;

        micBtn.addEventListener('click', () => {
            console.log("Speech Lifecycle: Main Mic Clicked.");
            toggleSpeech();
        });
        micBtn._hasListener = true;
        console.log("Speech Lifecycle: Main Mic Listener bound successfully.");
    }

    function initSpeechEngine() {
        // Lifecycle Logging
        console.log("Speech Lifecycle: Initializing Engine...");

        // Cleanup old instance if it exists
        if (recognition) {
            console.log("Speech Lifecycle: Cleaning up previous engine instance.");
            try {
                recognition.onstart = null;
                recognition.onend = null;
                recognition.onresult = null;
                recognition.onerror = null;
                recognition.abort();
            } catch (e) { console.warn("Speech cleanup error:", e); }
            recognition = null;
        }

        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            console.error("Speech Lifecycle: Web Speech API not supported in this browser.");
            return null;
        }

        const rec = new SpeechRecognition();
        rec.continuous = false;
        rec.interimResults = false;
        rec.maxAlternatives = 1;

        rec.onstart = () => {
            console.log("Speech Lifecycle: ONSTART - Recording started.");
            isRecording = true;
            state.isRecording = true;

            if (state.mode === 'conversation') {
                const btn = document.getElementById(`convMicBtn${state.activeSpeaker}`);
                if (btn) {
                    btn.classList.add('listening');
                    btn.querySelector('.mic-status').textContent = 'Listening...';
                }
            } else {
                if (micBtn) micBtn.classList.add('recording');
                const sourceText = document.getElementById('transSourceText');
                if (sourceText) {
                    sourceText.classList.add('listening');
                    sourceText.placeholder = 'Listening...';
                }
            }
        };

        rec.onend = () => {
            console.log("Speech Lifecycle: ONEND - Recording stopped.");
            isRecording = false;
            state.isRecording = false;

            if (state.mode === 'conversation') {
                const btn = document.getElementById(`convMicBtn${state.activeSpeaker}`);
                if (btn) {
                    btn.classList.remove('listening');
                    btn.querySelector('.mic-status').textContent = 'Tap to Speak';
                }
                processConversationInput();
            } else {
                if (micBtn) micBtn.classList.remove('recording');
                const sourceText = document.getElementById('transSourceText');
                if (sourceText) {
                    sourceText.classList.remove('listening');
                    sourceText.placeholder = 'Enter text...';
                }
                triggerTranslation();
            }

            // Cleanup engine after use to free resources
            setTimeout(() => {
                if (!isRecording && recognition === rec) {
                    recognition = null;
                }
            }, 100);
        };

        rec.onresult = (event) => {
            const transcript = Array.from(event.results)
                .map(result => result[0])
                .map(result => result.transcript)
                .join('');

            console.log("Speech Lifecycle: ONRESULT - Transcript received (Length:", transcript.length, ")");

            if (state.mode === 'conversation') {
                state.tempTranscript = transcript;
                addChatBubble(state.activeSpeaker, transcript + '...', 'local preview-only');
            } else {
                const sourceText = document.getElementById('transSourceText');
                if (sourceText) sourceText.value = transcript;
            }
        };

        rec.onerror = (event) => {

            let errorMessage = "Speech error.";
            let isSilent = false;

            if (event.error === 'no-speech') {
                console.info("Speech Recognition: No speech detected (Silence).");
                errorMessage = "No speech detected.";
                isSilent = true;
            } else if (event.error === 'network') {
                const isOffline = !navigator.onLine;
                console.error(`Speech Recognition Network Error: ${isOffline ? 'OFFLINE' : 'Browser connection to speech service failed'}`);
                errorMessage = isOffline ? "You are offline. Please check connection." : "Network Error: Speech service unreachable.";

                // STRICT FIX: Auto-Retry once on network error if online
                if (!isOffline && !state.hasRetried) {
                    console.info("Speech Recognition: Attempting automatic reconnection...");
                    state.hasRetried = true;
                    setTimeout(() => {
                        if (state.mode === 'conversation') startConversationMic(state.activeSpeaker);
                        else toggleSpeech();
                    }, 500);
                    return;
                }
                state.hasRetried = false; // Reset for manual clicks
            } else if (event.error === 'audio-capture') {
                console.error("Speech Recognition Error: No microphone found or access denied.");
                errorMessage = "Mic Error: No microphone found.";
            } else if (event.error === 'not-allowed') {
                console.error("Speech Recognition Error: Permission denied.");
                errorMessage = "Permission Denied.";
            } else {
                console.error("Speech Recognition Error:", event.error);
                errorMessage = `Error: ${event.error}`;
            }

            isRecording = false;
            state.isRecording = false; // Sync state

            if (state.mode === 'conversation') {
                // Clear preview if error
                const chatArea = document.getElementById(`convChat${state.activeSpeaker}`);
                const previewBubble = chatArea ? chatArea.querySelector('.chat-bubble.preview') : null;
                if (previewBubble) previewBubble.remove();

                const btn = document.getElementById(`convMicBtn${state.activeSpeaker}`);
                if (btn) {
                    btn.classList.remove('listening');
                    btn.querySelector('.mic-status').textContent = isSilent ? 'Tap to Speak' : errorMessage;

                    // Only show error state if NOT no-speech
                    if (!isSilent) {
                        btn.classList.add('error');
                        setTimeout(() => {
                            btn.classList.remove('error');
                            btn.querySelector('.mic-status').textContent = 'Tap to Speak';
                        }, 2000);
                    }
                }
            } else {
                if (micBtn) {
                    micBtn.classList.remove('recording');
                    if (!isSilent) {
                        micBtn.classList.add('error');
                        micBtn.setAttribute('title', errorMessage);
                        micBtn.setAttribute('aria-label', errorMessage);
                        setTimeout(() => micBtn.classList.remove('error'), 3000);
                    } else {
                        micBtn.setAttribute('aria-label', 'Start listening');
                    }
                }
                const sourceText = document.getElementById('transSourceText');
                if (sourceText) {
                    sourceText.classList.remove('listening');
                    sourceText.placeholder = isSilent ? "No speech detected. Try again?" : errorMessage;

                    // Temporary visual feedback in the input itself for critical errors
                    if (!isSilent && (event.error === 'network' || event.error === 'not-allowed')) {
                        const originalPlaceholder = "Enter text...";
                        setTimeout(() => {
                            if (sourceText.placeholder === errorMessage) {
                                sourceText.placeholder = originalPlaceholder;
                            }
                        }, 4000);
                    }
                }
            }
        };

        return rec;
    }


    async function checkPermissions() {
        if (navigator.permissions && navigator.permissions.query) {
            try {
                const result = await navigator.permissions.query({ name: 'microphone' });
                console.log("Speech Diagnostics: Microphone Permission State:", result.state);
                result.onchange = () => {
                    console.log("Speech Diagnostics: Microphone Permission Changed to:", result.state);
                };
            } catch (e) { console.warn("Speech Diagnostics: Permission query failed:", e); }
        }
    }

    function getEnhancedLangCode(code) {
        if (!code || code === 'auto') return navigator.language || 'en-US';

        const map = {
            'en': 'en-US',
            'es': 'es-ES',
            'fr': 'fr-FR',
            'de': 'de-DE',
            'gu': 'gu-IN',
            'hi': 'hi-IN',
            'ur': 'ur-PK',
            'bn': 'bn-IN',
            'ta': 'ta-IN',
            'te': 'te-IN',
            'mr': 'mr-IN',
            'kn': 'kn-IN',
            'ml': 'ml-IN',
            'pa': 'pa-IN',
            'ru': 'ru-RU',
            'ja': 'ja-JP',
            'ko': 'ko-KR',
            'zh': 'zh-CN',
            'pt': 'pt-BR',
            'it': 'it-IT',
            'tr': 'tr-TR',
            'nl': 'nl-NL'
        };
        return map[code] || code;
    }

    function toggleSpeech() {
        console.log("Mic button clicked. isRecording:", isRecording);

        if (isRecording && recognition) {
            recognition.stop();
            return;
        }

        // Connectivity Check
        if (!navigator.onLine) {
            console.warn("Speech Recognition: Browser is offline.");
            handleSpeechError('network');
            return;
        }

        // Set language with enhanced mapping
        const langCode = getEnhancedLangCode(state.sourceLang);
        console.log("Starting Fresh Speech Recognition for lang:", langCode);

        // Reset retry flag on manual start
        if (!state.hasRetried) state.hasRetried = false;

        recognition = initSpeechEngine();
        if (!recognition) return;

        recognition.lang = langCode;

        try {
            recognition.start();
        } catch (e) {
            console.error("Failed to start speech recognition", e);
            handleSpeechError('aborted');
        }
    }

    function handleSpeechError(errorType) {
        console.error(`Speech Lifecycle: ERROR detected - Type: ${errorType} | Mode: ${state.mode}`);

        // 1. Reset Internal State
        isRecording = false;
        state.isRecording = false;

        // 2. Mode-Aware UI Fixes
        if (state.mode === 'conversation' && state.activeSpeaker) {
            const btn = document.getElementById(`convMicBtn${state.activeSpeaker}`);
            if (btn) {
                btn.classList.remove('listening');
                btn.classList.add('error');
                btn.querySelector('.mic-status').textContent = 'Error - Try Again';
                setTimeout(() => {
                    btn.classList.remove('error');
                    btn.querySelector('.mic-status').textContent = 'Tap to Speak';
                }, 2000);
            }
        } else {
            const btn = document.getElementById('transMicBtn');
            if (btn) {
                btn.classList.remove('recording');
                btn.classList.add('error');
                setTimeout(() => btn.classList.remove('error'), 2000);
            }
            const sourceText = document.getElementById('transSourceText');
            if (sourceText) {
                sourceText.classList.remove('listening');
                sourceText.placeholder = 'Enter text...';
            }
        }

        // 3. Clear Stale Engine
        if (recognition) {
            try { recognition.abort(); } catch (e) { }
            recognition = null;
        }
    }




    // --- Conversation Mode Logic ---
    function toggleMode() {
        const toggle = document.getElementById('transModeToggle');
        setMode(toggle.checked ? 'conversation' : 'text');
    }

    function setMode(mode) {
        state.mode = mode;
        const toggle = document.getElementById('transModeToggle');
        const textView = document.getElementById('transTextView');
        const convView = document.getElementById('transConvView');
        const labelText = document.getElementById('modeTextLabel');
        const labelConv = document.getElementById('modeConvLabel');

        // Update UI
        if (mode === 'conversation') {
            textView.style.display = 'none';
            convView.style.display = 'flex'; // Flex for split view
            toggle.checked = true;
            labelText.classList.remove('active');
            labelConv.classList.add('active');

            // Sync languages if needed (first time)
            if (!state.convInitialized) {
                initConversationDropdowns();
                state.convInitialized = true;
            }
        } else {
            textView.style.display = 'block';
            convView.style.display = 'none';
            toggle.checked = false;
            labelText.classList.add('active');
            labelConv.classList.remove('active');
        }
    }

    function initConversationDropdowns() {
        const langList = languages.filter(l => l.code !== 'auto');
        setupConversationDropdown('A', langList);
        setupConversationDropdown('B', langList);

        // Listeners for Mics
        document.getElementById('convMicBtnA').addEventListener('click', () => toggleConversationMic('A'));
        document.getElementById('convMicBtnB').addEventListener('click', () => toggleConversationMic('B'));
    }

    function setupConversationDropdown(side, langList) {
        const wrapper = document.getElementById(`convLang${side}Wrapper`);
        const searchInput = document.getElementById(`convSearch${side}`);
        // Use NEW ID
        const optionsList = document.getElementById(`convOptionsList${side}New`);
        const label = document.getElementById(`convLang${side}Label`);
        const hiddenInput = document.getElementById(`convLang${side}`);

        if (!wrapper || !optionsList) return;

        // Render Options
        renderOptions(optionsList, langList, hiddenInput.value);

        // Event: Toggle
        const trigger = wrapper.querySelector('.custom-select-trigger');
        const customOptions = wrapper.querySelector('.custom-options');

        if (trigger) {
            trigger.addEventListener('click', (e) => {
                const wasOpen = wrapper.classList.contains('open');
                closeAllDropdowns(wrapper); // Close others

                if (!wasOpen) {
                    wrapper.classList.add('open');
                    // Force Styles
                    if (customOptions) {
                        customOptions.style.opacity = '1';
                        customOptions.style.visibility = 'visible';
                        customOptions.style.transform = 'translateY(0)';
                        customOptions.style.display = 'block';
                    }

                    searchInput.focus();
                    searchInput.value = '';
                    filterOptions(optionsList, langList, ''); // Reset filter
                } else {
                    wrapper.classList.remove('open');
                    // Reset Styles
                    if (customOptions) {
                        customOptions.style.opacity = '';
                        customOptions.style.visibility = '';
                        customOptions.style.transform = '';
                        customOptions.style.display = '';
                    }
                }
            });
        }

        // Event: Search
        searchInput.addEventListener('input', (e) => {
            filterOptions(optionsList, langList, e.target.value);
        });

        // Event: Option Click (Delegated)
        optionsList.addEventListener('click', (e) => {
            const option = e.target.closest('.custom-option');
            if (!option) return;

            const value = option.dataset.value;
            const name = option.dataset.name;

            // Update UI
            hiddenInput.value = value;
            label.textContent = name;

            wrapper.classList.remove('open');
            closeAllDropdowns(wrapper); // Ensure cleanup
            // Force reset of THIS wrapper
            if (customOptions) {
                customOptions.style.display = '';
                customOptions.style.visibility = '';
                customOptions.style.opacity = '';
            }

            // Update selected class
            Array.from(optionsList.children).forEach(c => c.classList.remove('selected'));
            option.classList.add('selected');
        });
    }

    function toggleMute() {
        state.isMuted = !state.isMuted;
        const icon = document.getElementById('convMuteIcon');
        icon.textContent = state.isMuted ? '🔇' : '🔊';
        window.speechSynthesis.cancel();
    }

    function clearChat() {
        document.getElementById('convChatA').innerHTML = '<div class="conv-placeholder">Tap mic to speak...</div>';
        document.getElementById('convChatB').innerHTML = '<div class="conv-placeholder">Tap mic to speak...</div>';
    }

    function toggleConversationMic(person) {
        console.log(`Speech Lifecycle: Conversation Mic (${person}) Clicked.`);

        // If already recording same person -> Stop
        if (isRecording && state.activeSpeaker === person && recognition) {
            console.log("Speech Lifecycle: Stopping current speaker recording.");
            recognition.stop();
            return;
        }

        // If recording other person -> Stop then Start
        if (isRecording && recognition) {
            console.log("Speech Lifecycle: Switching speakers. Stopping current.");
            recognition.stop();
            setTimeout(() => startConversationMic(person), 400);
            return;
        }

        startConversationMic(person);
    }


    function startConversationMic(person) {
        const langInputId = person === 'A' ? 'convLangA' : 'convLangB';
        const rawLang = document.getElementById(langInputId).value;
        const langCode = getEnhancedLangCode(rawLang);

        console.log(`Starting Fresh Translation Mic (${person}) for lang:`, langCode);

        state.activeSpeaker = person;

        // Reset retry flag on manual start
        if (!state.hasRetried) state.hasRetried = false;

        // Connectivity Check
        if (!navigator.onLine) {
            handleSpeechError('network');
            return;
        }

        recognition = initSpeechEngine();
        if (!recognition) return;

        recognition.lang = langCode;
        try {
            recognition.start();
        } catch (e) {
            console.error(e);
            handleSpeechError('aborted');
        }
    }


    async function processConversationInput() {
        const text = state.tempTranscript;

        // Remove preview bubble if exists
        const speaker = state.activeSpeaker;
        const chatArea = document.getElementById(`convChat${speaker}`);
        const previewBubble = chatArea.querySelector('.chat-bubble.preview');
        if (previewBubble) previewBubble.remove();

        if (!text) return;
        state.tempTranscript = ''; // Clear buffer

        const listener = speaker === 'A' ? 'B' : 'A';

        const langInputId = `convLang${speaker}`;
        const targetLangInputId = `convLang${listener}`;

        const sourceLang = document.getElementById(langInputId).value;
        const targetLang = document.getElementById(targetLangInputId).value;

        // 1. Add Source Bubble (Finalized)
        addChatBubble(speaker, text, 'local');

        // 2. Translate
        try {
            // Show typing/loading indicator? (For now, just wait)
            const response = await fetch(CONFIG.API_ENDPOINT, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    texts: [text],
                    sourceLang: sourceLang,
                    targetLang: targetLang
                })
            });

            if (response.ok) {
                const results = await response.json();
                if (results && results[0]) {
                    const translatedText = results[0].translatedText;

                    // 3. Add Target Bubble (Remote) on the OTHER side?
                    // Actually, usually conversation view shows history in one flow or local view.
                    // Let's mirror: A's chat shows A(Right) and B(Left).
                    // But we have split screen. 
                    // A's side shows A's text. B's side shows B's text (Translated).
                    // Let's add the translated text to the LISTENER'S side as a 'remote' bubble.

                    addChatBubble(listener, translatedText, 'remote', text); // Pass original as subtext?

                    // 4. Speak Output in Target Language
                    speakText(translatedText, targetLang);
                }
            }
        } catch (e) {
            console.error("Conversation Translation Error", e);
        }
    }

    function addChatBubble(side, text, type, subtext = null) {
        const chatArea = document.getElementById(`convChat${side}`);
        const bubble = document.createElement('div');
        bubble.className = `chat-bubble ${type}`;
        bubble.textContent = text;

        if (subtext) {
            const sub = document.createElement('div');
            sub.className = 'translation-sub';
            sub.textContent = subtext;
            bubble.appendChild(sub);
        }

        chatArea.appendChild(bubble);
        chatArea.scrollTop = chatArea.scrollHeight;

        // Hide placeholder
        const placeholder = chatArea.querySelector('.conv-placeholder');
        if (placeholder) placeholder.style.display = 'none';
    }

    function speakText(text, lang) {
        if (state.isMuted) return;

        // Reuse the robust logic from Speak Button but parameterized
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = lang;

        const voices = window.speechSynthesis.getVoices();
        let selectedVoice = voices.find(v => v.lang === lang);
        if (!selectedVoice) selectedVoice = voices.find(v => v.lang.startsWith(lang));
        // Name match fallback
        if (!selectedVoice) {
            const langObj = languages.find(l => l.code === lang);
            if (langObj) {
                selectedVoice = voices.find(v => v.name.toLowerCase().includes(langObj.name.toLowerCase()));
            }
        }

        if (selectedVoice) {
            utterance.voice = selectedVoice;
            window.speechSynthesis.speak(utterance);
        } else {
            // Fallback Google TTS
            try {
                const audio = new Audio(`https://translate.google.com/translate_tts?ie=UTF-8&q=${encodeURIComponent(text)}&tl=${lang}&client=tw-ob`);
                audio.play();
            } catch (e) { console.error(e); }
        }
    }
    // We need to modify setupSpeechRecognition to handle UI updates for Conversation Mode

    // ... Inside setupSpeechRecognition ...
    // Update onstart:
    /*
    recognition.onstart = () => {
        isRecording = true;
        state.isRecording = true;
        if (state.mode === 'conversation') {
            const btn = document.getElementById(`convMicBtn${state.activeSpeaker}`);
            if (btn) btn.classList.add('listening');
        } else {
            const micBtn = document.getElementById('transMicBtn');
            if (micBtn) micBtn.classList.add('recording');
            const sourceText = document.getElementById('transSourceText');
            if (sourceText) {
                sourceText.classList.add('listening');
                sourceText.placeholder = 'Listening...';
            }
        }
    };
     
    recognition.onend = () => {
        isRecording = false;
        state.isRecording = false;
        if (state.mode === 'conversation') {
             const btn = document.getElementById(`convMicBtn${state.activeSpeaker}`);
             if (btn) btn.classList.remove('listening');
             // Trigger Translation logic for Chat Bubble
             handleConversationResult();
        } else {
            // ... existing text mode logic ...
            const micBtn = document.getElementById('transMicBtn');
            if (micBtn) micBtn.classList.remove('recording');
            // ...
            triggerTranslation();
        }
    };
    */

    // Need to expose these to global object
    return {
        init: () => {
            init();
            if (window.speechSynthesis) {
                window.speechSynthesis.onvoiceschanged = () => { };
            }
        },
        toggleMode: toggleMode,
        setMode: setMode,
        toggleMute: toggleMute,
        clearChat: clearChat
    };
})();

// Initialization handled by Dashboard.cshtml
