// ==UserScript==
// @name         Wikipedia Tracker
// @namespace    http://tampermonkey.net/
// @version      1.1
// @description  Rejestruje aktywność użytkownika na Wikipedii
// @match        https://en.wikipedia.org/*
// @grant        none
// ==/UserScript==

(function() {
    'use strict';

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
                    console.log('old session', currentSection, duration);
                }
            }
            currentSection = section;
            sectionStartTime = Date.now();
        }
    }

    window.addEventListener('beforeunload', () => {
        if (currentSection && sectionStartTime) {
            const duration = (Date.now() - sectionStartTime) / 1000;
            sectionTimes.push({ title: currentSection, time: duration });
        }

        const totalTime = (Date.now() - startTime) / 1000;

        const data = {
            url: window.location.href,
            totalTime: totalTime,
            sections: sectionTimes,
            timestamp: new Date().toISOString()
        };

        navigator.sendBeacon('http://localhost:5000/log', JSON.stringify(data));
    });

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
})();
