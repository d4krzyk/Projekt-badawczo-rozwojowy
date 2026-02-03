// ==UserScript==
// @name         WikiSpeedrun Tracker
// @namespace    http://tampermonkey.net/
// @version      2.3
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
    let username = '';
    let group = '';

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
    /**
     * Get current time as an ISO 8601 string.
     * @returns {string} Current time in ISO format.
     */
    function nowISO() {
        return new Date().toISOString();
    }

    /**
     * Extract the article title from the current URL path.
     * If no title is found, returns the string 'unknown'.
     * @returns {string} Article title (decoded and spaces restored) or 'unknown'.
     */
    function getArticleTitle() {
        const match = location.pathname.match(/\/wiki\/(.+)$/);
        return match
            ? decodeURIComponent(match[1].replace(/_/g, ' '))
            : 'unknown';
    }

    /**
     * Locate the main article container element in the page DOM.
     * @returns {Element|null} The element with class 'mw-parser-output' or null if not found.
     */
    function getArticleContainer() {
        return document.querySelector('.mw-parser-output');
    }

    /**
     * Normalize a section heading element's text by removing bracketed
     * annotations (e.g. [edit]) and trimming whitespace.
     * @param {Element} el - The heading element (h2/h3/h4).
     * @returns {string} Cleaned section title.
     */
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

                gameEnded = false;

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
    /**
     * Handle entering an article page: record enter time, create a
     * room object for the session, and initialize section tracking
     * when article content becomes available.
     */
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

        waitForArticleContent((headings) => {
            hideUnwantedSections();
            initSectionTracking(headings);
        });
        setTimeout(hideUnwantedSections, 500);

    }

    /**
     * Handle exiting an article page: finalize current section,
     * set exit timestamp, copy section times to the page object and
     * clean up observers.
     */
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
    /**
     * Poll the DOM until the article container and headings are present,
     * then invoke the provided callback with the headings NodeList.
     * @param {function} callback - Function to call with the headings NodeList.
     */
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
    /**
     * Initialize an IntersectionObserver to monitor section headings
     * and update the active section as the user scrolls.
     * @param {NodeList} headings - List of heading elements to observe.
     */
    function initSectionTracking(headings) {
        cleanupSectionObserver();

        sectionObserver = new IntersectionObserver(onSectionIntersect, {
            root: null,
            rootMargin: '0px 0px -450px 0px',
            threshold: 0.5,
        });

        headings.forEach((h) => sectionObserver.observe(h));
    }

    /**
     * IntersectionObserver callback that determines the most centered
     * visible heading and starts/ends section timing accordingly.
     * @param {IntersectionObserverEntry[]} entries - Observer entries.
     */
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

    /**
     * Finalize timing for the currently active section and push a
     * session event into `sectionTimes` if the duration is >= 1s.
     */
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

    /**
     * Disconnect and clear the section IntersectionObserver if present.
     */
    function cleanupSectionObserver() {
        if (sectionObserver) {
            sectionObserver.disconnect();
            sectionObserver = null;
        }
    }

    /* ------------------------------
       KLIKNIĘCIA LINKÓW
    --------------------------------*/
    /**
     * Record a clicked link (or auxiliary click) into the current room's
     * `book_links` array with a timestamp.
     * @param {Event} event - The click/auxclick event.
     */
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
    /**
     * Merge consecutive or duplicate room entries by name into a single
     * aggregated room, preserving enter/exit times, links and combining
     * 'Introduction' events.
     * @param {Array} rooms - Array of room objects to merge.
     * @returns {Array} Array of merged room objects.
     */
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

    /**
     * Convert internal room objects to a compact session log format that
     * uses seconds relative to the session start.
     * @param {Array} rooms - Array of room objects.
     * @param {string} sessionStartISO - ISO timestamp of session start.
     * @returns {Array} Array of mapped session log objects.
     */
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

    /**
     * Finalize and send the session to the configured API endpoint.
     * Merges rooms, maps logs and issues a POST request.
     * @param {string} [reason='unknown'] - Reason code for session end.
     */
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
            group: group,
            surrendered: reason === 'surrender_modal_button',
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
    /**
     * Set the username used for session reporting. Validates input type.
     * @param {string} name - Username to set.
     */
    function setName(name) {
        if (!name || typeof name !== 'string') {
            console.error('setName(name): name musi być stringiem');
            return;
        }

        username = name.trim();
        console.log(`WikiTracker: ustawiono username = "${username}"`);
    }

    /**
     * Set the group used for session reporting. Validates input type.
     * @param {string} name - Group name to set.
     */
    function setGroup(name) {
        if (!name || typeof name !== 'string') {
            console.error('setGroup(name): name musi być stringiem');
            return;
        }

        group = name.trim();
        console.log(`WikiTracker: ustawiono group = "${group}"`);
    }

    unsafeWindow.setName = setName;

    /**
     * Inject a username input field into the game's start form if not
     * already present. Persists value to localStorage and wires change events.
     */
    function injectUsernameField() {
        const form = document.querySelector(
            'form.flex.max-w-\\[650px\\].flex-col.gap-4'
        );
        if (!form) return;
        if (form.querySelector('#ws-username')) return;

        const wrapper = document.createElement('div');
        wrapper.className = 'flex min-w-52 flex-1 flex-col';

        const label = document.createElement('label');
        label.innerText = 'Username';
        label.htmlFor = 'ws-username';

        const container = document.createElement('div');
        container.className = 'css-b62m3t-container';

        // accessibility spans
        const liveRegion1 = document.createElement('span');
        liveRegion1.className = 'css-7pg0cj-a11yText';
        liveRegion1.id = 'ws-username-live-region';

        const liveRegion2 = document.createElement('span');
        liveRegion2.className = 'css-7pg0cj-a11yText';
        liveRegion2.setAttribute('aria-live', 'polite');
        liveRegion2.setAttribute('aria-atomic', 'false');
        liveRegion2.setAttribute('aria-relevant', 'additions text');
        liveRegion2.setAttribute('role', 'log');

        // input wrapper
        const controlDiv = document.createElement('div');
        controlDiv.className = 'dark:bg-dark-surface dark:text-dark-primary css-1a3xvlw-control';

        const innerDiv = document.createElement('div');
        innerDiv.className = 'css-hlgwow';

        const inputDiv = document.createElement('div');
        inputDiv.className = 'dark:text-dark-primary';

        const input = document.createElement('input');
        input.id = 'ws-username';
        input.type = 'text';
        input.placeholder = 'Wpisz swój nick';
        input.autocomplete = 'off';
        input.className = '';
        input.style.color = 'inherit';
        input.style.background = '0px center';
        input.style.opacity = '1';
        input.style.width = '100%';
        input.style.gridArea = '1 / 2';
        input.style.font = 'inherit';
        input.style.minWidth = '2px';
        input.style.border = '0px';
        input.style.margin = '0px';
        input.style.outline = '0px';
        input.style.padding = '0px';

        // wczytanie ostatniej wartości
        const saved = localStorage.getItem('wikispeedrun_username');
        if (saved) {
            input.value = saved;
            setName(saved);
        }

        input.addEventListener('input', () => {
            const val = input.value.trim();
            localStorage.setItem('wikispeedrun_username', val);
            setName(val);
        });

        inputDiv.appendChild(input);
        innerDiv.appendChild(inputDiv);
        controlDiv.appendChild(innerDiv);

        const indicatorDiv = document.createElement('div');
        indicatorDiv.className = 'css-1wy0on6';
        const indicatorInner = document.createElement('div');
        indicatorInner.className = 'css-1xc3v61-indicatorContainer';
        indicatorDiv.appendChild(indicatorInner);
        controlDiv.appendChild(indicatorDiv);

        container.appendChild(liveRegion1);
        container.appendChild(liveRegion2);
        container.appendChild(controlDiv);

        wrapper.appendChild(label);
        wrapper.appendChild(container);

        form.prepend(wrapper);

        console.log('%cWikiTracker: dodano pole Username', 'color: cyan');
    }

    /**
     * Inject a group input field into the game's start form if not
     * already present. Persists value to localStorage and wires change events.
     */
    function injectGroupField() {
        const form = document.querySelector(
            'form.flex.max-w-\\[650px\\].flex-col.gap-4'
        );
        if (!form) return;
        if (form.querySelector('#ws-group')) return;

        const wrapper = document.createElement('div');
        wrapper.className = 'flex min-w-52 flex-1 flex-col';

        const label = document.createElement('label');
        label.innerText = 'Grupa';
        label.htmlFor = 'ws-group';

        const container = document.createElement('div');
        container.className = 'css-b62m3t-container';

        // accessibility spans
        const liveRegion1 = document.createElement('span');
        liveRegion1.className = 'css-7pg0cj-a11yText';
        liveRegion1.id = 'ws-group-live-region';

        const liveRegion2 = document.createElement('span');
        liveRegion2.className = 'css-7pg0cj-a11yText';
        liveRegion2.setAttribute('aria-live', 'polite');
        liveRegion2.setAttribute('aria-atomic', 'false');
        liveRegion2.setAttribute('aria-relevant', 'additions text');
        liveRegion2.setAttribute('role', 'log');

        // input wrapper
        const controlDiv = document.createElement('div');
        controlDiv.className = 'dark:bg-dark-surface dark:text-dark-primary css-1a3xvlw-control';

        const innerDiv = document.createElement('div');
        innerDiv.className = 'css-hlgwow';

        const inputDiv = document.createElement('div');
        inputDiv.className = 'dark:text-dark-primary';

        const input = document.createElement('input');
        input.id = 'ws-group';
        input.type = 'text';
        input.placeholder = 'Wpisz swoją grupę';
        input.autocomplete = 'off';
        input.className = '';
        input.style.color = 'inherit';
        input.style.background = '0px center';
        input.style.opacity = '1';
        input.style.width = '100%';
        input.style.gridArea = '1 / 2';
        input.style.font = 'inherit';
        input.style.minWidth = '2px';
        input.style.border = '0px';
        input.style.margin = '0px';
        input.style.outline = '0px';
        input.style.padding = '0px';

        // wczytanie ostatniej wartości
        const saved = localStorage.getItem('wikispeedrun_group');
        if (saved) {
            input.value = saved;
            setGroup(saved);
        }

        input.addEventListener('input', () => {
            const val = input.value.trim();
            localStorage.setItem('wikispeedrun_group', val);
            setGroup(val);
        });

        inputDiv.appendChild(input);
        innerDiv.appendChild(inputDiv);
        controlDiv.appendChild(innerDiv);

        const indicatorDiv = document.createElement('div');
        indicatorDiv.className = 'css-1wy0on6';
        const indicatorInner = document.createElement('div');
        indicatorInner.className = 'css-1xc3v61-indicatorContainer';
        indicatorDiv.appendChild(indicatorInner);
        controlDiv.appendChild(indicatorDiv);

        container.appendChild(liveRegion1);
        container.appendChild(liveRegion2);
        container.appendChild(controlDiv);

        wrapper.appendChild(label);
        wrapper.appendChild(container);

        form.prepend(wrapper);

        console.log('%cWikiTracker: dodano pole Group', 'color: cyan');
    }

    const formObserver = new MutationObserver(() => {
        injectUsernameField();
        injectGroupField();
    });

    formObserver.observe(document.body, {
        childList: true,
        subtree: true,
    });


    /* ------------------------------
       UKRYWANIE UI
    --------------------------------*/

    /**
     * Hide the main UI elements used by the game (used when session starts).
     */
    function hideUI() {
        const elements = document.querySelectorAll('.flex.h-full.w-full.flex-col.items-center.justify-start.gap-8');
        elements.forEach(el => {
            el.style.display = 'none';
        })
    }

    /**
     * Show the main UI elements (reverses `hideUI`).
     */
    function showUI() {
        const elements = document.querySelectorAll('.flex.h-full.w-full.flex-col.items-center.justify-start.gap-8');
        elements.forEach(el => {
            el.style.display = 'flex';
        })
    }

    unsafeWindow.hideUI = hideUI;
    unsafeWindow.showUI = showUI;

    /* ------------------------------
       UKRYWANIE SEKCJI
    --------------------------------*/

    /**
     * Remove undesired sections from the article (references, notes,
     * external links, etc.) and optionally remove tables.
     */
    function hideUnwantedSections() {
        const titles = [
            'references',
            'notes',
            'sources',
            'external links',
            'see also',
        ];

        const container = document.querySelector('.mw-parser-output');
        if (!container) return;

        const headings = container.querySelectorAll('h2');

        headings.forEach(h2 => {
            const title = h2.innerText.trim().toLowerCase();

            if (!titles.includes(title)) return;

            let el = h2.parentElement.nextElementSibling;

            h2.remove();

            while (el) {
                const next = el.nextElementSibling;

                if (
                    el.nodeType === Node.ELEMENT_NODE &&
                    el.tagName === 'H2'
                ) {
                    break;
                }

                el.remove();
                el = next;
            }
        });

        removeTables();  // optional
    }

    /**
     * Remove non-infobox tables from the article container to reduce noise
     * during section tracking.
     */
    function removeTables() {
        const container = document.querySelector('.mw-parser-output');
        if (!container) return;

        const tables = container.querySelectorAll('table');

        tables.forEach(table => {
            if (table.classList.contains('infobox')) return;
            table.remove();
        });
    }
})();
