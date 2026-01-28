/**
 * Language Translator Widget V3
 * Handles:
 * - Custom Searchable Dropdowns
 * - Theme-aware updates
 * - Auto-detect and Translation Logic
 * - Copy/Speak Interaction
 */
const TranslatorWidget = (function () {
    const CONFIG = {
        API_ENDPOINT: '/api/Translation/batch',
        DEBOUNCE_MS: 1000
    };

    const state = {
        sourceLang: 'auto',
        targetLang: 'en',
        typingTimer: null,
        isTranslating: false
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
    let detectedSpan, actionBtn, copyBtn, speakBtn;

    // Custom Dropdown Elements
    let sourceWrapper, sourceSearch, sourceOptionsList, sourceLabel, sourceHidden;
    let targetWrapper, targetSearch, targetOptionsList, targetLabel, targetHidden;

    function init() {
        sourceInput = document.getElementById('transSourceText');
        targetInput = document.getElementById('transTargetText');
        swapBtn = document.getElementById('transSwapBtn');
        statusIndicator = document.getElementById('transStatus');
        detectedSpan = document.getElementById('transDetectedLang');
        actionBtn = document.getElementById('transActionBtn');
        copyBtn = document.getElementById('transCopyBtn');
        speakBtn = document.getElementById('transSpeakBtn');

        // Dropdown Logic
        initDropdown('source', languages);
        initDropdown('target', languages.filter(l => l.code !== 'auto'));

        setupEventListeners();
    }

    function initDropdown(type, langList) {
        const wrapper = document.getElementById(`${type}LangWrapper`);
        const searchInput = document.getElementById(`${type}Search`);
        const optionsList = document.getElementById(`${type}OptionsList`);
        const label = document.getElementById(`${type}LangLabel`);
        const hiddenInput = document.getElementById(`trans${type.charAt(0).toUpperCase() + type.slice(1)}Lang`);

        if (!wrapper) return;

        // Store references
        if (type === 'source') {
            sourceWrapper = wrapper;
            sourceSearch = searchInput;
            sourceOptionsList = optionsList;
            sourceLabel = label;
            sourceHidden = hiddenInput;
        } else {
            targetWrapper = wrapper;
            targetSearch = searchInput;
            targetOptionsList = optionsList;
            targetLabel = label;
            targetHidden = hiddenInput;
        }

        // Render Options
        renderOptions(optionsList, langList, hiddenInput.value);

        // Event: Toggle
        wrapper.querySelector('.custom-select-trigger').addEventListener('click', (e) => {
            closeAllDropdowns(wrapper); // Close others
            wrapper.classList.toggle('open');
            if (wrapper.classList.contains('open')) {
                searchInput.focus();
                searchInput.value = '';
                filterOptions(optionsList, langList, ''); // Reset filter
            }
        });

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
        container.innerHTML = list.map(l => `
            <div class="custom-option ${l.code === selectedValue ? 'selected' : ''}" 
                 data-value="${l.code}" 
                 data-name="${l.name}">
                 ${l.name} <span style="opacity:0.5; font-size:0.8em; margin-left:4px;">${l.native}</span>
            </div>
        `).join('');
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
            if (w !== except) w.classList.remove('open');
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
        console.log("Setting up Speech Recognition...");
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        const micBtn = document.getElementById('transMicBtn');

        if (!micBtn) {
            console.error("Microphone button 'transMicBtn' NOT FOUND in DOM");
            return;
        }

        if (!SpeechRecognition) {
            console.warn("Speech Recognition API not supported in this browser.");
            micBtn.style.display = 'none';
            return;
        }

        console.log("Speech Recognition API supported. Button found.");

        recognition = new SpeechRecognition();
        recognition.continuous = false; // Stop after silence
        recognition.interimResults = true; // Show text while speaking

        recognition.onstart = () => {
            isRecording = true;
            if (micBtn) {
                micBtn.classList.add('recording');
                micBtn.classList.remove('error');
                micBtn.setAttribute('aria-label', 'Stop listening');
            }
            sourceInput.placeholder = "Listening...";
            sourceInput.classList.add('listening');
        };

        recognition.onend = () => {
            isRecording = false;
            if (micBtn) {
                micBtn.classList.remove('recording');
                micBtn.setAttribute('aria-label', 'Start listening');
            }
            sourceInput.placeholder = "Enter text...";
            sourceInput.classList.remove('listening');

            // Only trigger if we have text
            if (sourceInput.value.trim().length > 0) {
                triggerTranslation();
            }
        };

        recognition.onresult = (event) => {
            let transcript = '';
            for (let i = event.resultIndex; i < event.results.length; i++) {
                transcript += event.results[i][0].transcript;
            }
            sourceInput.value = transcript;
        };

        recognition.onerror = (event) => {
            console.error("Speech Recognition Error", event.error);
            isRecording = false;

            if (micBtn) {
                micBtn.classList.remove('recording');
                micBtn.classList.add('error');
                micBtn.setAttribute('aria-label', 'Error. Try again.');

                // Clear error animation after a bit
                setTimeout(() => micBtn.classList.remove('error'), 1000);
            }

            sourceInput.classList.remove('listening');

            // User-friendly feedback
            switch (event.error) {
                case 'not-allowed':
                    sourceInput.placeholder = "Mic permission denied.";
                    alert("Please allow microphone access to use voice input.");
                    break;
                case 'no-speech':
                    sourceInput.placeholder = "No speech detected. Try again.";
                    break;
                case 'network':
                    sourceInput.placeholder = "Network error.";
                    break;
                default:
                    sourceInput.placeholder = "Error listening.";
            }
        };

        if (micBtn) {
            micBtn.addEventListener('click', toggleSpeech);
        }
    }

    function toggleSpeech() {
        console.log("Mic button clicked. isRecording:", isRecording);
        if (!recognition) return;

        if (isRecording) {
            recognition.stop();
        } else {
            // Set language based on source selection
            const langCode = state.sourceLang === 'auto' ? navigator.language : state.sourceLang;
            recognition.lang = langCode;

            try {
                recognition.start();
            } catch (e) {
                console.error("Failed to start speech recognition", e);
            }
        }
    }

    return {
        init: () => {
            init(); // Existing init
            setupSpeechRecognition(); // New Voice Init

            // Preload voices (Chrome sometimes loads them asynchronously)
            if (window.speechSynthesis) {
                window.speechSynthesis.onvoiceschanged = () => {
                    console.log("Voices loaded:", window.speechSynthesis.getVoices().length);
                };
            }
        }
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', TranslatorWidget.init);
} else {
    TranslatorWidget.init();
}
