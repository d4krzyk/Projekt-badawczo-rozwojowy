// ==UserScript==
// @name         Wikipedia Tracker
// @namespace    http://tampermonkey.net/
// @version      1.2
// @description  Rejestruje aktywność użytkownika na Wikipedii
// @match        https://en.wikipedia.org/*
// @grant        none
// ==/UserScript==

(function() {
    'use strict';

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
       PROFILOWANIE CZASU W SEKCJACH
    --------------------------------*/

    const startTime = Date.now();
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
                const duration = (Date.now() - sectionStartTime) / 1000;
                if (duration > 1) {
                    sectionTimes.push({
                        title: currentSection,
                        time: duration
                    });
                    console.log('old section:', currentSection, duration);
                }
            }
            currentSection = section;
            sectionStartTime = Date.now();
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
            const duration = (Date.now() - sectionStartTime) / 1000;
            sectionTimes.push({
                title: currentSection,
                time: duration
            });
        }

        const totalTime = (Date.now() - startTime) / 1000;

        const data = {
            url: window.location.href,
            totalTime: totalTime,
            sections: sectionTimes,
            timestamp: new Date().toISOString(),
            navigationType: navigationType,
            entrySource: entrySource
        };

        navigator.sendBeacon('http://localhost:5000/log', JSON.stringify(data));
    });

})();
