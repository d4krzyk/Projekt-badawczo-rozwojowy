// ==UserScript==
// @name         WikiSpeedrun Tracker
// @namespace    http://tampermonkey.net/
// @version      2.0.1
// @description  Track full WikiSpeedrun session with sections timing
// @match        https://wikispeedrun.org/*
// @connect      localhost
// @grant        GM_xmlhttpRequest
// ==/UserScript==

(function () {
    'use strict';

    /* ------------------------------
       KONFIGURACJA
    --------------------------------*/
    const API_LOG = 'http://localhost/session/';

    /* ------------------------------
       UŻYTKOWNIK
    --------------------------------*/
    let username = null;

    /* ------------------------------
       STAN SESJI
    --------------------------------*/
    let session = {
        active: false,
        start_time: null,
        end_time: null,
        rooms: [],
    };

    let currentPage = null;
    let pageEnterTime = null;

    /* ------------------------------
       SEKCJE
    --------------------------------*/
    let sectionObserver = null;
    let currentSection = null;
    let sectionStartTime = null;
    let sectionTimes = [];

    /* ------------------------------
       UTILS
    --------------------------------*/
    function nowISO() {
        return new Date().toISOString();
    }

    function getArticleTitle() {
        const match = location.pathname.match(/\/wiki\/(.+)$/);
        return match
            ? decodeURIComponent(match[1].replace(/_/g, ' '))
            : 'unknown';
    }

    function getArticleContainer() {
        return document.querySelector('.mw-parser-output');
    }

    function getSectionTitle(el) {
        return el.innerText.replace(/\[.*?\]/g, '').trim();
    }

    /* ------------------------------
       START GRY
    --------------------------------*/
    document.addEventListener(
        'submit',
        (event) => {
            if (event.target.innerText.includes('Graj')) {
                console.log(`%cWikiTracker: start gry`, "color: green; font-weight: bold;");

                session = {
                    active: true,
                    start_time: nowISO(),
                    end_time: null,
                    rooms: [],
                };

                trackPageEnter();
                currentSection = 'Introduction';
                sectionStartTime = new Date();
                hideUI();
            }
        },
        true
    );

    /* ------------------------------
       SPA – ZMIANA URL
    --------------------------------*/
    (function (history) {
        const pushState = history.pushState;
        const replaceState = history.replaceState;

        function onUrlChange() {
            if (!session.active) return;
            trackPageExit();
            trackPageEnter();
        }

        history.pushState = function () {
            pushState.apply(history, arguments);
            onUrlChange();
        };

        history.replaceState = function () {
            replaceState.apply(history, arguments);
            onUrlChange();
        };

        window.addEventListener('popstate', onUrlChange);
    })(window.history);

    /* ------------------------------
       STRONA – ENTER / EXIT
    --------------------------------*/
    function trackPageEnter() {
        pageEnterTime = new Date();
        currentPage = {
            name: getArticleTitle(),
            url: location.href,
            enter_time: pageEnterTime.toISOString(),
            exit_time: null,
            books: [],
            book_links: [],
        };

        session.rooms.push(currentPage);
        console.log('Artykuł:', currentPage.name);

        waitForArticleContent(initSectionTracking);
    }

    function trackPageExit() {
        if (!currentPage) return;

        endCurrentSection();

        currentPage.exit_time = nowISO();
        currentPage.books = sectionTimes;

        cleanupSectionObserver();

        sectionTimes = [];
        currentSection = 'Introduction';
        sectionStartTime = new Date();

        console.log('Exit artykułu:', currentPage.name);
    }

    /* ------------------------------
       CZEKAJ NA DYNAMICZNY CONTENT
    --------------------------------*/
    function waitForArticleContent(callback) {
        const interval = setInterval(() => {
            const container = getArticleContainer();
            const headings = container?.querySelectorAll('h2, h3, h4');

            if (container && headings && headings.length > 0) {
                clearInterval(interval);
                callback(headings);
            }
        }, 200);
    }

    /* ------------------------------
       ŚLEDZENIE SEKCJI
    --------------------------------*/
    function initSectionTracking(headings) {
        cleanupSectionObserver();

        sectionObserver = new IntersectionObserver(onSectionIntersect, {
            root: null,
            rootMargin: '0px 0px -450px 0px',
            threshold: 0.5,
        });

        headings.forEach((h) => sectionObserver.observe(h));
    }

    function onSectionIntersect(entries) {
        const visible = entries
            .filter((e) => e.isIntersecting)
            .map((e) => ({
                el: e.target,
                distance:
                    Math.abs(
                        e.boundingClientRect.top +
                            e.boundingClientRect.height / 2 -
                            window.innerHeight / 2
                    ),
            }))
            .sort((a, b) => a.distance - b.distance);

        if (visible.length === 0) return;

        const title = getSectionTitle(visible[0].el);

        if (currentSection !== title) {
            endCurrentSection();
            currentSection = title;
            sectionStartTime = new Date();
            console.log('Sekcja:', title);
        }
    }

    function endCurrentSection() {
        if (!currentSection || !sectionStartTime) return;

        const duration = (new Date() - sectionStartTime) / 1000;
        if (duration < 1) return;

        sectionTimes.push({
            name: currentSection,
            session_events: [
                {
                    open_time: sectionStartTime.toISOString(),
                    close_time: nowISO(),
                    duration_sec: duration,
                },
            ],
        });
    }

    function cleanupSectionObserver() {
        if (sectionObserver) {
            sectionObserver.disconnect();
            sectionObserver = null;
        }
    }

    /* ------------------------------
       KLIKNIĘCIA LINKÓW
    --------------------------------*/
    function logLinkInteraction(event) {
        if (!session.active) return;

        const link = event.target.closest('a');
        if (!link || !link.href) return;

        const lastRoom = session.rooms[session.rooms.length - 1];
        if (!lastRoom) return;

        lastRoom.book_links.push({
            link: link.href,
            click_time: nowISO(),
        });
    }

    // 1. mouse + Ctrl/Cmd
    document.addEventListener('click', (e) => {
        logLinkInteraction(e);
    });

    // 2. Auxiliary click
    document.addEventListener('auxclick', (e) => {
        if (e.button === 1) { // 1 to środkowy przycisk
            logLinkInteraction(e);
        }
    });

    /* ------------------------------
        WYKRYWANIE KOŃCA GRY
    --------------------------------*/
    let gameEnded = false;

    const endGameObserver = new MutationObserver((mutations) => {
        if (!session.active || gameEnded) return;

        for (const mutation of mutations) {
            for (const node of mutation.addedNodes) {
                if (!(node instanceof HTMLElement)) continue;

                const dialog =
                      node.getAttribute?.('role') === 'dialog'
                ? node
                : node.querySelector?.('[role="dialog"]');

                if (!dialog) continue;

                const text = dialog.innerText || '';

                if (text.includes('Zagraj ponownie')) {
                    console.log(`%cWikiTracker: wykryto koniec gry`, "color: orange; font-weight: bold;");
                    gameEnded = true;
                    endSession('game_finished_modal');
                    return;
                }
            }
        }
    });

    endGameObserver.observe(document.body, {
        childList: true,
        subtree: true,
    });

    /* ------------------------------
       KONIEC SESJI
    --------------------------------*/
    function mergeDuplicateRooms(rooms) {
        const byName = new Map();

        for (const room of rooms) {
            if (!byName.has(room.name)) {
                byName.set(room.name, []);
            }
            byName.get(room.name).push(room);
        }

        const mergedRooms = [];

        for (const [name, instances] of byName.entries()) {
            if (instances.length === 1) {
                mergedRooms.push(instances[0]);
                continue;
            }

            const introOnly = instances.filter(
                r =>
                    Array.isArray(r.books) &&
                    r.books.length === 1 &&
                    r.books[0].name === 'Introduction'
            );

            const full = instances.filter(
                r => !introOnly.includes(r)
            );

            if (full.length > 0) {
                const all = [...introOnly, ...full];

                const enterTimes = all.map(r => new Date(r.enter_time));
                const exitTimes = all.map(r => new Date(r.exit_time));

                const merged = {
                    ...full[0],
                    enter_time: new Date(Math.min(...enterTimes)).toISOString(),
                    exit_time: new Date(Math.max(...exitTimes)).toISOString(),
                    book_links: all.flatMap(r => r.book_links || []),
                    books: [],
                };

                const introEvents = all
                    .flatMap(r => r.books)
                    .filter(b => b.name === 'Introduction')
                    .flatMap(b => b.session_events);

                if (introEvents.length > 0) {
                    const openTimes = introEvents.map(e => new Date(e.open_time));
                    const closeTimes = introEvents.map(e => new Date(e.close_time));

                    merged.books.push({
                        name: 'Introduction',
                        session_events: [{
                            open_time: new Date(Math.min(...openTimes)).toISOString(),
                            close_time: new Date(Math.max(...closeTimes)).toISOString(),
                            duration_sec:
                                (Math.max(...closeTimes) - Math.min(...openTimes)) / 1000,
                        }],
                    });
                }

                full.forEach(r => {
                    r.books.forEach(b => {
                        if (b.name !== 'Introduction') {
                            merged.books.push(b);
                        }
                    });
                });

                mergedRooms.push(merged);
            } else {
                mergedRooms.push(instances[0]);
            }
        }

        return mergedRooms;
    }

    function mapRoomsToSessionLogs(rooms, sessionStartISO) {
        const sessionStartMs = Date.parse(sessionStartISO);

        return rooms.map(room => {
            const enterMs = Date.parse(room.enter_time);
            const exitMs = Date.parse(room.exit_time);

            return {
                roomName: room.name,
                enterTime: (enterMs - sessionStartMs) / 1000,
                exitTime: (exitMs - sessionStartMs) / 1000,
                bookLogs: (room.books || []).flatMap(book =>
                    (book.session_events || []).map(ev => ({
                        bookName: book.name,
                        openTime:
                            (Date.parse(ev.open_time) - sessionStartMs) / 1000,
                        closeTime:
                            (Date.parse(ev.close_time) - sessionStartMs) / 1000,
                    }))
                ),

                linkLogs: (room.book_links || []).map(link => ({
                    linkName: link.link,
                    clickTime:
                        (Date.parse(link.click_time) - sessionStartMs) / 1000,
                })),
            };
        });
    }


    document.addEventListener('click', (e) => {
        if (!session.active || gameEnded) return;

        const btn = e.target.closest('button');
        if (!btn) return;

        const text = btn.innerText?.trim();
        if (text !== 'Poddaj się') return;

        const modal = btn.closest('div[role="dialog"][data-state="open"]');
        if (!modal) return;

        console.log(`%cWikiTracker: gracz poddał się`, "color: orangered; font-weight: bold;");

        gameEnded = true;
        endSession('surrender_modal_button');
    });

    function endSession(reason = 'unknown') {
        if (!session.active) return;

        trackPageExit();

        session.end_time = nowISO();
        session.active = false;
        session.reason = reason;

        session.rooms = mergeDuplicateRooms(
            session.rooms.filter(
                room => Array.isArray(room.books) && room.books.length > 0
            )
        );

        const data = {
            user_name: username,
            session_logs: mapRoomsToSessionLogs(session.rooms, session.start_time),
        };

        console.log('Wysyłam sesję:', data);

        fetch(API_LOG, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Web': 'true'
            },
            body: JSON.stringify(data),
            keepalive: true
        });
    }

    unsafeWindow.finishSession = endSession;

    /* ------------------------------
       UŻYTKOWNIK
    --------------------------------*/
    function setName(name) {
        if (!name || typeof name !== 'string') {
            console.error('setName(name): name musi być stringiem');
            return;
        }

        username = name.trim();
        console.log(`WikiTracker: ustawiono username = "${username}"`);
    }

    unsafeWindow.setName = setName;

    /* ------------------------------
       UKRYWANIE UI
    --------------------------------*/

    function hideUI() {
        const elements = document.querySelectorAll('.flex.h-full.w-full.flex-col.items-center.justify-start.gap-8');
        elements.forEach(el => {
            el.style.display = 'none';
        })
    }

    function showUI() {
        const elements = document.querySelectorAll('.flex.h-full.w-full.flex-col.items-center.justify-start.gap-8');
        elements.forEach(el => {
            el.style.display = 'flex';
        })
    }

    unsafeWindow.hideUI = hideUI;
    unsafeWindow.showUI = showUI;
})();
