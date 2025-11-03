// ==UserScript==
// @name         Wikipedia Tracker
// @namespace    http://tampermonkey.net/
// @version      1.0
// @description  Rejestruje czas użytkownika na Wikipedii
// @match        https://en.wikipedia.org/*
// @grant        none
// ==/UserScript==

(function() {
    'use strict';

    const startTime = Date.now();

    window.addEventListener('beforeunload', () => {
        const duration = (Date.now() - startTime) / 1000;
        const data = {
            url: window.location.href,
            timeSpent: duration,
            timestamp: new Date().toISOString()
        };
        navigator.sendBeacon('http://localhost:5000/log', JSON.stringify(data));
    });
})();
