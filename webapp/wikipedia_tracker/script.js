// ==UserScript==
// @name         Wikipedia Tracker
// @namespace    http://tampermonkey.net/
// @version      0.5
// @description  Record user activity on Wikipedia and send data to the API
// @match        https://en.wikipedia.org/*
// @connect      localhost
// @grant        GM_xmlhttpRequest
// @grant        unsafeWindow
// ==/UserScript==

(function() {
    'use strict';

    /* ------------------------------
       KONFIGURACJA API
    --------------------------------*/
    const API_BASE = 'http://localhost/tracker';
    const API_SESSIONS = `${API_BASE}/create-session`;
    const API_LOG = `${API_BASE}/log`;

    /* ------------------------------
       FUNKCJE WYKRYWANIA ŹRÓDŁA WEJŚCIA
    --------------------------------*/

    function getNavigationType() {
        const nav = performance.getEntriesByType("navigation")[0];
        if (!nav) return "unknown";

        switch (nav.type) {
            case "navigate":     return "new";       // otwarcie linkiem, nowa karta, wpisanie adresu
            case "reload":       return "reload";    // F5, Ctrl+R, przycisk odświeżenia
            case "back_forward": return "history";   // wejście z historii (wstecz/przód)
            default:             return nav.type;
        }
    }

    function getEntrySource() {
        const ref = document.referrer;

        if (!ref) return "direct";                      // nowa karta / wpisanie URL / otwarcie z zakładek
        if (ref.includes("wikipedia.org")) return "internal-link"; // kliknięty link na Wikipedii
        return "external-referrer";                     // np. Google, Facebook, inne strony
    }

    const navigationType = getNavigationType();
    const entrySource = getEntrySource();


    /* ------------------------------
       ŚLEDZENIE KLIKNIĘTYCH LINKÓW
    --------------------------------*/

    const clickedLinks = [];

    function logLinkInteraction(event, interactionType) {
        const linkElement = event.target.closest('a');

        if (linkElement && linkElement.href) {
            clickedLinks.push({
                link: linkElement.href,
                click_time: new Date().toISOString(),
            });
        }
    }

    // 1. mouse + Ctrl/Cmd
    document.addEventListener('click', (e) => {
        let type = 'left-click';
        if (e.ctrlKey || e.metaKey) type = 'ctrl-left-click';
        if (e.shiftKey) type = 'shift-left-click';
        logLinkInteraction(e, type);
    });

    // 2. Auxiliary click
    document.addEventListener('auxclick', (e) => {
        if (e.button === 1) { // 1 to zazwyczaj środkowy przycisk
            logLinkInteraction(e, 'middle-click');
        }
    });

    // 3. Opening context menu
    document.addEventListener('contextmenu', (e) => {
        logLinkInteraction(e, 'right-click-context');
    });

    /* ------------------------------
       PROFILOWANIE CZASU W SEKCJACH
    --------------------------------*/

    const startTime = new Date();
    const sectionTimes = [];
    let currentSection = null;
    let sectionStartTime = null;

    const headings = document.querySelectorAll('h2, h3, h4');

    function getSectionTitle(el) {
        return el.innerText.replace(/\[edit\]/g, '').trim();
    }

    function onSectionEnter(section) {
        if (currentSection !== section) {
            if (currentSection && sectionStartTime) {
                const duration = (new Date() - sectionStartTime) / 1000;
                if (duration > 1) {
                    sectionTimes.push({
                        name: currentSection,
                        session_events: [{
                            open_time: sectionStartTime.toISOString(),
                            close_time: new Date().toISOString(),
                        }],
                    });
                    console.log('old section:', currentSection, duration);
                }
            }
            currentSection = section;
            sectionStartTime = new Date();
        }
    }

    const observer = new IntersectionObserver((entries) => {
        let visible = entries
            .filter(e => e.isIntersecting)
            .map(e => ({
                element: e.target,
                distance: Math.abs(e.boundingClientRect.top + e.boundingClientRect.height/2 - window.innerHeight/2)
            }))
            .sort((a, b) => a.distance - b.distance);

        if (visible.length > 0) {
            const sectionTitle = getSectionTitle(visible[0].element);
            onSectionEnter(sectionTitle);
        }
    }, {
        root: null,
        rootMargin: '0px',
        threshold: [0.5]
    });

    headings.forEach(h => observer.observe(h));


    /* ------------------------------
       WYSYŁANIE DANYCH PRZY WYJŚCIU
    --------------------------------*/

    window.addEventListener('beforeunload', () => {
        if (currentSection && sectionStartTime) {
            sectionTimes.push({
                name: currentSection,
                session_events: [{
                    open_time: sectionStartTime,
                    close_time: new Date().toISOString(),
                }],
            });
        }

        const data = {
            name: window.location.href,
            enter_time: startTime.toISOString(),
            exit_time: new Date().toISOString(),
            books: sectionTimes,
            book_links: clickedLinks,
            extra_data: {
                timestamp: new Date().toISOString(),
                navigationType: navigationType,
                entrySource: entrySource
            },
        };
        const blob = new Blob([JSON.stringify(data)], { type: 'application/json' });

        navigator.sendBeacon(API_LOG, blob);
    });

    /* ------------------------------
       TWORZENIE SESJI Z KONSOLI
    --------------------------------*/

    function createSessionLogic(sessionName) {
         if (!sessionName) {
            console.error("❌ Musisz podać nazwę sesji! Np: start('MojaSesja')");
            return;
        }

        console.log(`⏳ Wysyłam żądanie utworzenia sesji: "${sessionName}"...`);

        GM_xmlhttpRequest({
            method: "POST",
            url: API_SESSIONS,
            data: sessionName,
            headers: {
                "Content-Type": "text/plain"
            },
            onload: function(response) {
                if (response.status >= 200 && response.status < 300) {
                    const data = JSON.parse(response.responseText);
                    console.log(`%c✅ Sukces! Sesja "${sessionName}" utworzona`, "color: green; font-weight: bold;");
                } else {
                    console.error("❌ Błąd serwera:", response.status, response.responseText);
                }
            },
            onerror: function(err) {
                console.error("❌ Błąd połączenia z backendem.", err);
            }
        });
    }

    unsafeWindow.start = createSessionLogic;

    console.log("%cWikipedia Tracker gotowy. Wpisz start('Nazwa') aby zakończyć poprzednią sesję i rozpocząć nową.", "color: green");
})();
