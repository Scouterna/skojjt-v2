// ==UserScript==
// @name         Skojjt → Sensus Attendance Sync
// @namespace    https://e-tjanst.sensus.se/
// @version      0.4.0
// @description  Syncs meeting attendance from Skojjt to Sensus e-tjänst
// @author       Skojjt
// @match        https://e-tjanst.sensus.se/*
// @run-at       document-idle
// @icon         https://www.google.com/s2/favicons?sz=64&domain=sensus.se
// @grant        GM_xmlhttpRequest
// @grant        GM_getValue
// @grant        GM_setValue
// @grant        GM_registerMenuCommand
// @grant        GM_addStyle
// @grant        unsafeWindow
// @connect      skojjt.scouterna.net
// ==/UserScript==

(function () {
    'use strict';

    // =========================================================================
    // Constants
    // =========================================================================
    const SKOJJT_BASE_URL = 'https://skojjt.scouterna.net';
    const CONFIG_KEYS = {
        scoutGroupId: 'skojjt_scoutGroupId',
        scoutGroupName: 'skojjt_scoutGroupName',
        troopId: 'skojjt_troopId',
        troopName: 'skojjt_troopName',
        sensusUsername: 'sensus_username',
        sensusPassword: 'sensus_password',
    };

    // Cached user info from /api/v1/me
    let cachedMe = null;

    // =========================================================================
    // Config helpers
    // =========================================================================
    function getConfig(key) {
        return GM_getValue(key, '');
    }

    function setConfig(key, value) {
        GM_setValue(key, value);
    }

    function getAllConfig() {
        const cfg = {};
        for (const [name, key] of Object.entries(CONFIG_KEYS)) {
            cfg[name] = getConfig(key);
        }
        return cfg;
    }

    // =========================================================================
    // Semester utilities
    // =========================================================================
    /**
     * Calculate the current semester ID based on today's date.
     * Semester ID = Year * 10 + (isAutumn ? 1 : 0)
     * Spring (VT) = Jan-Jul, Autumn (HT) = Aug-Dec
     */
    function getCurrentSemesterId() {
        const now = new Date();
        const year = now.getFullYear();
        const isAutumn = now.getMonth() >= 7; // Aug (7) through Dec (11)
        return year * 10 + (isAutumn ? 1 : 0);
    }

    function getCurrentSemesterDisplayName() {
        const now = new Date();
        const year = now.getFullYear();
        const isAutumn = now.getMonth() >= 7;
        return isAutumn ? `HT ${year}` : `VT ${year}`;
    }

    // =========================================================================
    // Skojjt API client (cross-origin via GM_xmlhttpRequest with cookies)
    // =========================================================================
    function skojjtRequest(method, path, data) {
        return new Promise((resolve, reject) => {
            GM_xmlhttpRequest({
                method,
                url: `${SKOJJT_BASE_URL}${path}`,
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                },
                // Send cookies for skojjt.scouterna.net (ScoutID session)
                anonymous: false,
                data: data ? JSON.stringify(data) : undefined,
                onload(response) {
                    if (response.status >= 200 && response.status < 300) {
                        try {
                            resolve(JSON.parse(response.responseText));
                        } catch {
                            resolve(response.responseText);
                        }
                    } else if (response.status === 401 || response.status === 302) {
                        reject(new Error('NOT_AUTHENTICATED'));
                    } else {
                        reject(new Error(`Skojjt API ${response.status}: ${response.responseText}`));
                    }
                },
                onerror(err) {
                    reject(new Error(`Skojjt API network error: ${err.error || 'unknown'}`));
                },
            });
        });
    }

    /**
     * Check Skojjt authentication and get user info.
     * @returns {Promise<{uid:string, displayName:string, email:string, isMemberRegistrar:boolean, accessibleGroups:{id:number, name:string}[], accessibleTroopScoutnetIds:number[]}>}
     */
    function fetchMe() {
        return skojjtRequest('GET', '/api/v1/me');
    }

    /** @returns {Promise<{id:number, scoutnetId:number, name:string, semesterId:number, semesterDisplayName:string, memberCount:number}[]>} */
    function fetchTroops(scoutGroupId, semesterId) {
        const params = new URLSearchParams();
        if (scoutGroupId) params.set('scoutGroupId', scoutGroupId);
        if (semesterId) params.set('semesterId', semesterId);
        const qs = params.toString();
        return skojjtRequest('GET', `/api/v1/troops${qs ? '?' + qs : ''}`);
    }

    /** @returns {Promise<{id:number, firstName:string, lastName:string, fullName:string, age:number|null, isRemoved:boolean}[]>} */
    function fetchTroopMembers(troopId) {
        return skojjtRequest('GET', `/api/v1/persons/troop/${troopId}`);
    }

    /** @returns {Promise<{id:number, troopId:number, meetingDate:string, startTime:string, name:string, durationMinutes:number, isHike:boolean, attendanceCount:number}[]>} */
    function fetchMeetings(troopId) {
        return skojjtRequest('GET', `/api/v1/meetings?troopId=${troopId}`);
    }

    /** @returns {Promise<{id:number, troopId:number, troopName:string, meetingDate:string, startTime:string, name:string, durationMinutes:number, isHike:boolean, attendingPersonIds:number[]}>} */
    function fetchMeetingDetail(meetingId) {
        return skojjtRequest('GET', `/api/v1/meetings/${meetingId}`);
    }

    // =========================================================================
    // Name matching utilities
    // =========================================================================
    function normalizeName(name) {
        return (name || '')
            .trim()
            .toLowerCase()
            .replace(/\s+/g, ' ');
    }

    // =========================================================================
    // Sensus API client (page-context fetch to share session cookies)
    // Use unsafeWindow.fetch so the page's cookies are sent with requests.
    // Tampermonkey's sandboxed fetch does NOT share the page's cookie jar.
    // =========================================================================
    const pageFetch = (typeof unsafeWindow !== 'undefined' ? unsafeWindow : window).fetch.bind(
        typeof unsafeWindow !== 'undefined' ? unsafeWindow : window
    );

    async function sensusGet(path, params) {
        const url = new URL(`/api${path}`, window.location.origin);
        if (params) {
            for (const [key, value] of Object.entries(params)) {
                url.searchParams.set(key, String(value));
            }
        }
        const response = await pageFetch(url.toString(), {
            method: 'GET',
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' },
        });
        if (response.status === 401) {
            throw new Error('SENSUS_AUTH: Du är inte inloggad i Sensus. Logga in på e-tjanst.sensus.se först.');
        }
        if (!response.ok) throw new Error(`Sensus GET ${response.status}: ${path}`);
        return response.json();
    }

    async function sensusPut(arrangemangId, schema) {
        const formData = new FormData();
        formData.set('data', JSON.stringify(schema));
        const response = await pageFetch(`/api/arrangemangs/${arrangemangId}/schemas/${schema.id}`, {
            method: 'PUT',
            credentials: 'same-origin',
            body: formData,
        });
        if (!response.ok) {
            const text = await response.text().catch(() => '');
            throw new Error(`Sensus PUT ${response.status}: schema ${schema.id} — ${text}`);
        }
        return response.json();
    }

    /** Fetch arrangemang list for attendance registration */
    async function fetchSensusArrangemang() {
        return sensusGet('/arrangemangs', {
            size: 100,
            page: 1,
            view: 4,            // AttRegistreraNärvaro
            verksar: '0',       // All years (string per Sensus SPA)
            listtype: 0,        // User
            arrtypfilters: 0,   // All types
            narvarofilter: 1,   // "Alla närvarolistor"
            sorttype: 9,        // Default sort (startdatum ascending)
            getProgress: true,
        });
    }

    /** Fetch participants for an arrangemang */
    async function fetchSensusDeltagare(arrangemangId) {
        // Sensus SPA uses &-prefixed query string in the path (not ?), e.g.
        // GET /api/arrangemangs/{id}/arrdeltagares/&roll=0&dolda=false
        return sensusGet(`/arrangemangs/${arrangemangId}/arrdeltagares/&roll=0&dolda=false`);
    }

    /** Fetch schemas (sammankomster) for an arrangemang */
    async function fetchSensusSchemas(arrangemangId) {
        return sensusGet(`/arrangemangs/${arrangemangId}/schema`);
    }

    /** Try to detect arrangemang ID from the current Sensus URL */
    function detectArrangemangId() {
        const match = window.location.pathname.match(/\/registrera-narvaro-signera\/(\d+)/);
        return match ? parseInt(match[1]) : null;
    }

    /**
     * Normalize date to YYYY-MM-DD for comparison.
     * Handles both "YYYY-MM-DD" (Skojjt DateOnly) and ISO datetime strings.
     */
    function normalizeDate(dateStr) {
        if (!dateStr) return null;
        // If already YYYY-MM-DD, return as-is
        if (/^\d{4}-\d{2}-\d{2}$/.test(dateStr)) return dateStr;
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) return null;
        return d.toISOString().split('T')[0];
    }

    /**
     * Extract array from a Sensus API response that might be
     * a plain array or a paginated { items: [...] } wrapper.
     */
    function extractItems(response) {
        if (Array.isArray(response)) return response;
        if (response && Array.isArray(response.result)) return response.result;
        if (response && Array.isArray(response.items)) return response.items;
        if (response && Array.isArray(response.data)) return response.data;
        return [];
    }

    // =========================================================================
    // Logging / Status overlay
    // =========================================================================
    let statusEl = null;
    let logEl = null;

    function createStatusOverlay() {
        const container = document.createElement('div');
        container.id = 'skojjt-status';
        container.innerHTML = `
            <div id="skojjt-status-header">
                <span>Skojjt → Sensus</span>
                <button id="skojjt-status-close">✕</button>
            </div>
            <div id="skojjt-status-text">Redo</div>
            <div id="skojjt-log"></div>
            <div id="skojjt-actions">
                <button id="skojjt-btn-sync" class="skojjt-btn">▶ Synka närvaro</button>
                <button id="skojjt-btn-config" class="skojjt-btn skojjt-btn-secondary">⚙ Inställningar</button>
            </div>
        `;
        document.body.appendChild(container);

        statusEl = document.getElementById('skojjt-status-text');
        logEl = document.getElementById('skojjt-log');

        document.getElementById('skojjt-status-close').addEventListener('click', () => {
            container.style.display = container.style.display === 'none' ? 'block' : 'none';
        });
        document.getElementById('skojjt-btn-config').addEventListener('click', showConfigPanel);
        document.getElementById('skojjt-btn-sync').addEventListener('click', startSync);
    }

    function setStatus(text) {
        if (statusEl) statusEl.textContent = text;
    }

    function log(msg, level = 'info') {
        if (!logEl) return;
        const line = document.createElement('div');
        line.className = `skojjt-log-${level}`;
        line.textContent = `[${new Date().toLocaleTimeString('sv-SE')}] ${msg}`;
        logEl.appendChild(line);
        logEl.scrollTop = logEl.scrollHeight;
        console.log(`[Skojjt ${level}] ${msg}`);
    }

    // =========================================================================
    // Skojjt login helper
    // =========================================================================
    function openSkojjtLogin() {
        log('Öppnar Skojjt-inloggning i ny flik...');
        window.open(`${SKOJJT_BASE_URL}/auth/challenge?returnUrl=/`, '_blank');
    }

    // =========================================================================
    // Config panel UI
    // =========================================================================
    let configPanelEl = null;

    async function showConfigPanel() {
        if (configPanelEl) {
            configPanelEl.remove();
            configPanelEl = null;
            return;
        }

        const cfg = getAllConfig();

        const panel = document.createElement('div');
        panel.id = 'skojjt-config';
        panel.innerHTML = `
            <div id="skojjt-config-header">
                <span>⚙ Skojjt Inställningar</span>
                <button id="skojjt-config-close">✕</button>
            </div>
            <div class="skojjt-config-body">
                <div id="cfg-auth-section">
                    <div id="cfg-auth-status">Kontrollerar Skojjt-inloggning...</div>
                </div>
                <label>Scoutkår
                    <select id="cfg-group"><option value="">Laddar...</option></select>
                </label>
                <label>Avdelning
                    <select id="cfg-troop"><option value="">Välj scoutkår först</option></select>
                </label>
                <div id="cfg-semester-info" style="font-size:12px; color:#a6adc8; margin-bottom:12px;"></div>
                <hr />
                <label>Sensus personnummer
                    <input type="text" id="cfg-sensus-user" value="${escapeHtml(cfg.sensusUsername)}" placeholder="YYYYMMDDNNNN" />
                </label>
                <label>Sensus lösenord
                    <input type="password" id="cfg-sensus-pass" value="${escapeHtml(cfg.sensusPassword)}" />
                </label>
                <div class="skojjt-config-actions">
                    <button id="cfg-save" class="skojjt-btn">Spara</button>
                </div>
                <div id="cfg-status"></div>
            </div>
        `;
        document.body.appendChild(panel);
        configPanelEl = panel;

        document.getElementById('skojjt-config-close').addEventListener('click', () => {
            panel.remove();
            configPanelEl = null;
        });

        document.getElementById('cfg-save').addEventListener('click', saveConfig);

        // When group changes, reload troops
        document.getElementById('cfg-group').addEventListener('change', () => {
            loadTroopsForGroup(cfg);
        });

        await checkAuthAndLoadData(cfg);
    }

    async function checkAuthAndLoadData(cfg) {
        const authSection = document.getElementById('cfg-auth-section');
        const groupSelect = document.getElementById('cfg-group');
        const semesterInfo = document.getElementById('cfg-semester-info');

        try {
            const me = await fetchMe();
            cachedMe = me;

            authSection.innerHTML = `
                <div style="display:flex; align-items:center; gap:8px; margin-bottom:12px; padding:8px 10px; background:#313244; border-radius:6px;">
                    <span style="color:#a6e3a1;">✓</span>
                    <span>Inloggad som <b>${escapeHtml(me.displayName)}</b></span>
                </div>
            `;

            // Show current semester
            semesterInfo.textContent = `Termin: ${getCurrentSemesterDisplayName()} (automatisk)`;

            // Populate scout group dropdown
            if (me.accessibleGroups.length === 0) {
                groupSelect.innerHTML = '<option value="">Inga scoutkårer tillgängliga</option>';
                return;
            }

            groupSelect.innerHTML = '';
            if (me.accessibleGroups.length > 1) {
                const placeholder = document.createElement('option');
                placeholder.value = '';
                placeholder.textContent = '-- Välj scoutkår --';
                groupSelect.appendChild(placeholder);
            }
            for (const g of me.accessibleGroups) {
                const opt = document.createElement('option');
                opt.value = g.id;
                opt.textContent = g.name;
                if (String(g.id) === String(cfg.scoutGroupId)) opt.selected = true;
                groupSelect.appendChild(opt);
            }

            // Auto-select if only one group
            if (me.accessibleGroups.length === 1) {
                groupSelect.value = me.accessibleGroups[0].id;
            }

            // Load troops for selected group
            await loadTroopsForGroup(cfg);

        } catch (err) {
            if (err.message === 'NOT_AUTHENTICATED') {
                cachedMe = null;
                authSection.innerHTML = `
                    <div style="margin-bottom:12px; padding:10px; background:#45475a; border-radius:6px;">
                        <div style="color:#f9e2af; margin-bottom:8px;">Inte inloggad i Skojjt</div>
                        <div style="font-size:12px; color:#a6adc8; margin-bottom:8px;">
                            Logga in på Skojjt via ScoutID i en annan flik, sedan kom tillbaka hit.
                        </div>
                        <button id="cfg-login-btn" class="skojjt-btn" style="width:100%;">
                            🔑 Logga in på Skojjt
                        </button>
                    </div>
                `;
                document.getElementById('cfg-login-btn').addEventListener('click', openSkojjtLogin);
                groupSelect.innerHTML = '<option value="">Logga in först</option>';
            } else {
                authSection.innerHTML = `
                    <div style="color:#f38ba8; margin-bottom:12px; padding:8px 10px; background:#313244; border-radius:6px;">
                        Anslutningsfel: ${escapeHtml(err.message)}
                    </div>
                `;
            }
        }
    }

    async function loadTroopsForGroup(cfg) {
        const groupSelect = document.getElementById('cfg-group');
        const troopSelect = document.getElementById('cfg-troop');
        const statusDiv = document.getElementById('cfg-status');

        const groupId = groupSelect?.value;
        if (!groupId) {
            troopSelect.innerHTML = '<option value="">Välj scoutkår först</option>';
            return;
        }

        try {
            troopSelect.innerHTML = '<option value="">Laddar avdelningar...</option>';
            const semesterId = getCurrentSemesterId();
            const troops = await fetchTroops(groupId, semesterId);

            troopSelect.innerHTML = '';
            if (troops.length === 0) {
                troopSelect.innerHTML = `<option value="">Inga avdelningar för ${getCurrentSemesterDisplayName()}</option>`;
                return;
            }

            if (troops.length > 1) {
                const placeholder = document.createElement('option');
                placeholder.value = '';
                placeholder.textContent = '-- Välj avdelning --';
                troopSelect.appendChild(placeholder);
            }

            for (const t of troops) {
                const opt = document.createElement('option');
                opt.value = t.id;
                opt.dataset.troopName = t.name;
                opt.textContent = `${t.name} (${t.memberCount} medlemmar)`;
                if (String(t.id) === String(cfg.troopId)) opt.selected = true;
                troopSelect.appendChild(opt);
            }

            // Auto-select if only one troop
            if (troops.length === 1) {
                troopSelect.value = troops[0].id;
            }

            if (statusDiv) {
                statusDiv.textContent = `Hämtade ${troops.length} avdelningar`;
                statusDiv.style.color = '#a6adc8';
            }
        } catch (err) {
            troopSelect.innerHTML = '<option value="">Fel vid hämtning</option>';
            if (statusDiv) {
                statusDiv.textContent = `Fel: ${err.message}`;
                statusDiv.style.color = '#f38ba8';
            }
        }
    }

    function saveConfig() {
        const groupSelect = document.getElementById('cfg-group');
        const troopSelect = document.getElementById('cfg-troop');

        setConfig(CONFIG_KEYS.scoutGroupId, groupSelect.value);
        const groupOption = groupSelect.options[groupSelect.selectedIndex];
        if (groupOption) setConfig(CONFIG_KEYS.scoutGroupName, groupOption.textContent);

        setConfig(CONFIG_KEYS.troopId, troopSelect.value);
        const troopOption = troopSelect.options[troopSelect.selectedIndex];
        if (troopOption) setConfig(CONFIG_KEYS.troopName, troopOption.dataset.troopName || troopOption.textContent);

        setConfig(CONFIG_KEYS.sensusUsername, document.getElementById('cfg-sensus-user').value.trim());
        setConfig(CONFIG_KEYS.sensusPassword, document.getElementById('cfg-sensus-pass').value);

        const statusDiv = document.getElementById('cfg-status');
        statusDiv.textContent = 'Sparat!';
        statusDiv.style.color = '#4caf50';
        log('Inställningar sparade');

        // Update main panel status
        updateReadyStatus();
    }

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str || '';
        return div.innerHTML;
    }

    // =========================================================================
    // Sensus login automation
    // =========================================================================
    function isLoginPage() {
        const passwordInput = document.querySelector('input[type="password"]');
        const usernameInput = document.querySelector('input[type="text"]');
        return !!(passwordInput && usernameInput);
    }

    function attemptAutoLogin() {
        const username = getConfig(CONFIG_KEYS.sensusUsername);
        const password = getConfig(CONFIG_KEYS.sensusPassword);

        if (!username || !password) {
            log('Sensus-inloggning: Inga uppgifter sparade. Öppna inställningar.', 'warn');
            setStatus('Logga in i Sensus eller konfigurera inloggning');
            return;
        }

        log('Fyller i inloggningsuppgifter...');
        setStatus('Loggar in i Sensus...');

        const usernameInput = document.querySelector('input[type="text"]');
        const passwordInput = document.querySelector('input[type="password"]');

        if (usernameInput && passwordInput) {
            setInputValue(usernameInput, username);
            setInputValue(passwordInput, password);

            const loginBtn = document.querySelector('button[type="submit"], input[type="submit"], .login-button, button');
            if (loginBtn) {
                log('Klickar på logga in...');
                setTimeout(() => loginBtn.click(), 300);
            } else {
                log('Kunde inte hitta inloggningsknappen', 'warn');
            }
        }
    }

    function setInputValue(input, value) {
        const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
            window.HTMLInputElement.prototype, 'value'
        ).set;
        nativeInputValueSetter.call(input, value);
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // =========================================================================
    // Attendance sync
    // =========================================================================
    async function startSync() {
        const troopId = getConfig(CONFIG_KEYS.troopId);
        if (!troopId) {
            log('Ingen avdelning vald. Öppna inställningar.', 'warn');
            setStatus('Konfigurera avdelning först');
            return;
        }

        setStatus('Hämtar data från Skojjt...');
        log(`Startar synk för avdelning ${getConfig(CONFIG_KEYS.troopName) || troopId}`);

        try {
            // 1. Fetch members and meetings from Skojjt
            const [members, meetingSummaries] = await Promise.all([
                fetchTroopMembers(parseInt(troopId)),
                fetchMeetings(parseInt(troopId)),
            ]);
            log(`Hämtade ${members.length} medlemmar och ${meetingSummaries.length} sammankomster`);

            // 2. Fetch full attendance for each meeting
            const meetings = [];
            for (const summary of meetingSummaries) {
                const detail = await fetchMeetingDetail(summary.id);
                meetings.push(detail);
            }
            log(`Hämtade närvarodetaljer för ${meetings.length} sammankomster`);

            // 3. Build a date -> attendance map
            const attendanceByDate = new Map();
            for (const m of meetings) {
                attendanceByDate.set(m.meetingDate, {
                    meeting: m,
                    attendingIds: new Set(m.attendingPersonIds),
                });
            }

            log('Skojjt-data redo. Söker Sensus-arrangemang...', 'info');

            // 4. Detect or select Sensus arrangemang
            setStatus('Söker Sensus-arrangemang...');
            let arrangemangId = detectArrangemangId();

            if (arrangemangId) {
                log(`Arrangemang från URL: ${arrangemangId}`);
            } else {
                log('Inte på arrangemangssida, hämtar lista...');
                const arrangemangResponse = await fetchSensusArrangemang();
                const arrangemangList = extractItems(arrangemangResponse);

                if (arrangemangList.length === 0) {
                    log('Inga arrangemang hittades för närvaroregistrering.', 'error');
                    setStatus('Inga arrangemang hittades');
                    return;
                }

                arrangemangId = arrangemangList[0].id;
                log(`Hittade ${arrangemangList.length} arrangemang`);
                for (const a of arrangemangList) {
                    log(`  • [${a.id}] ${a.namn || a.name || '(namnlös)'}`);
                }
                log(`Använder: [${arrangemangId}] ${arrangemangList[0].namn || arrangemangList[0].name || ''}`);
            }

            // 5. Fetch Sensus deltagare and schemas
            setStatus('Hämtar Sensus-data...');
            const [sensusDeltagareRaw, sensusSchemasRaw] = await Promise.all([
                fetchSensusDeltagare(arrangemangId),
                fetchSensusSchemas(arrangemangId),
            ]);
            const sensusDeltagare = extractItems(sensusDeltagareRaw);
            const sensusSchemas = extractItems(sensusSchemasRaw);

            log(`Sensus: ${sensusDeltagare.length} deltagare, ${sensusSchemas.length} sammankomster`);

            if (sensusDeltagare.length === 0) {
                log('Inga deltagare i Sensus-arrangemanget.', 'error');
                setStatus('Inga Sensus-deltagare');
                return;
            }

            // 6. Build Sensus name → person.id mapping
            const sensusPersonMap = new Map(); // normalizedName -> sensus person.id
            for (const d of sensusDeltagare) {
                const name = d.person
                    ? `${d.person.fornamn} ${d.person.efternamn}`
                    : d.namn;
                const pid = d.person?.id ?? d.id;
                sensusPersonMap.set(normalizeName(name), pid);
            }

            // 7. Match Skojjt members to Sensus persons
            const skojjtToSensus = new Map(); // skojjt person id -> sensus person id
            const unmatchedSkojjt = [];
            const usedSensusNames = new Set();

            for (const member of members) {
                const normalizedFull = normalizeName(member.fullName);
                const normalizedParts = normalizeName(`${member.firstName} ${member.lastName}`);
                let sensusId = null;
                let matchedName = null;

                // Direct full name match
                if (sensusPersonMap.has(normalizedFull)) {
                    sensusId = sensusPersonMap.get(normalizedFull);
                    matchedName = normalizedFull;
                }
                // Direct first+last name match
                else if (sensusPersonMap.has(normalizedParts)) {
                    sensusId = sensusPersonMap.get(normalizedParts);
                    matchedName = normalizedParts;
                }
                // Reversed name (Sensus might show "Lastname Firstname")
                else {
                    const reversed = normalizeName(`${member.lastName} ${member.firstName}`);
                    if (sensusPersonMap.has(reversed)) {
                        sensusId = sensusPersonMap.get(reversed);
                        matchedName = reversed;
                    } else {
                        // Partial/fuzzy match
                        for (const [sName, sId] of sensusPersonMap) {
                            if (usedSensusNames.has(sName)) continue;
                            if (sName.includes(normalizedFull) || normalizedFull.includes(sName) ||
                                sName.includes(normalizedParts) || normalizedParts.includes(sName)) {
                                sensusId = sId;
                                matchedName = sName;
                                break;
                            }
                        }
                    }
                }

                if (sensusId !== null && matchedName) {
                    skojjtToSensus.set(member.id, sensusId);
                    usedSensusNames.add(matchedName);
                } else {
                    unmatchedSkojjt.push(member.fullName);
                }
            }

            log(`Matchade ${skojjtToSensus.size} av ${members.length} personer`);
            if (unmatchedSkojjt.length > 0) {
                log(`Omatchade Skojjt: ${unmatchedSkojjt.join(', ')}`, 'warn');
            }
            const unmatchedSensusNames = [...sensusPersonMap.keys()]
                .filter(n => !usedSensusNames.has(n));
            if (unmatchedSensusNames.length > 0) {
                log(`Omatchade Sensus: ${unmatchedSensusNames.join(', ')}`, 'warn');
            }

            // 8. Build Sensus schema date index
            const schemaByDate = new Map();
            for (const schema of sensusSchemas) {
                const date = normalizeDate(schema.datum);
                if (date) {
                    schemaByDate.set(date, schema);
                } else {
                    log(`Schema ${schema.id} har ogiltigt datum: ${schema.datum}`, 'warn');
                }
            }

            // 9. Sync attendance for each matching date
            setStatus('Synkar närvaro...');
            let syncedCount = 0;
            let skippedCount = 0;
            let errorCount = 0;
            let noMatchCount = 0;

            for (const [skojjtDate, data] of attendanceByDate) {
                const normalizedSkojjtDate = normalizeDate(skojjtDate);
                const schema = schemaByDate.get(normalizedSkojjtDate);

                if (!schema) {
                    log(`${skojjtDate}: inget matchande Sensus-schema`, 'warn');
                    noMatchCount++;
                    continue;
                }

                if (schema.signerad) {
                    log(`${skojjtDate}: redan signerad, hoppar över`, 'warn');
                    skippedCount++;
                    continue;
                }

                if (schema.redigerbar === false) {
                    log(`${skojjtDate}: inte redigerbar, hoppar över`, 'warn');
                    skippedCount++;
                    continue;
                }

                // Build narvaros: Sensus person IDs of attending Skojjt members
                const narvaros = [];
                let unmatchedAttendees = 0;
                for (const skojjtId of data.attendingIds) {
                    const sensusId = skojjtToSensus.get(skojjtId);
                    if (sensusId !== undefined) {
                        narvaros.push(sensusId);
                    } else {
                        unmatchedAttendees++;
                    }
                }

                // Clone and update schema
                const updatedSchema = JSON.parse(JSON.stringify(schema));
                updatedSchema.narvaros = narvaros;
                updatedSchema.signeratAntalStudieTimmar = 1;

                try {
                    await sensusPut(arrangemangId, updatedSchema);
                    const suffix = unmatchedAttendees > 0
                        ? ` (${unmatchedAttendees} omatchade)`
                        : '';
                    log(`✓ ${skojjtDate}: ${narvaros.length} närvarande${suffix}`);
                    syncedCount++;
                } catch (err) {
                    log(`✗ ${skojjtDate}: ${err.message}`, 'error');
                    errorCount++;
                }
            }

            // Summary
            const parts = [];
            if (syncedCount > 0) parts.push(`${syncedCount} synkade`);
            if (skippedCount > 0) parts.push(`${skippedCount} hoppade över`);
            if (noMatchCount > 0) parts.push(`${noMatchCount} utan matchande datum`);
            if (errorCount > 0) parts.push(`${errorCount} fel`);
            const summary = `Synk klar! ${parts.join(', ')}`;
            log(summary, errorCount > 0 ? 'warn' : 'info');
            setStatus(summary);

        } catch (err) {
            if (err.message === 'NOT_AUTHENTICATED') {
                log('Inte inloggad i Skojjt. Logga in först.', 'error');
                setStatus('Inte inloggad i Skojjt');
                openSkojjtLogin();
            } else if (err.message?.startsWith('SENSUS_AUTH:')) {
                const msg = err.message.replace('SENSUS_AUTH: ', '');
                log(msg, 'error');
                setStatus('Inte inloggad i Sensus');
            } else {
                log(`Fel: ${err.message}`, 'error');
                setStatus('Fel vid synkronisering');
                console.error(err);
            }
        }
    }

    // =========================================================================
    // Status helpers
    // =========================================================================
    function updateReadyStatus() {
        const cfg = getAllConfig();
        if (!cfg.troopId) {
            setStatus('Välj avdelning i inställningar');
        } else {
            setStatus(`Redo — ${cfg.troopName || 'Avdelning ' + cfg.troopId}`);
        }
    }

    // =========================================================================
    // Styles
    // =========================================================================
    GM_addStyle(`
        #skojjt-status {
            position: fixed;
            bottom: 16px;
            right: 16px;
            width: 380px;
            max-height: 500px;
            background: #1e1e2e;
            color: #cdd6f4;
            border-radius: 10px;
            box-shadow: 0 4px 24px rgba(0,0,0,0.4);
            z-index: 99999;
            font-family: 'Segoe UI', system-ui, sans-serif;
            font-size: 13px;
            overflow: hidden;
        }
        #skojjt-status-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 10px 14px;
            background: #313244;
            font-weight: 600;
            font-size: 14px;
        }
        #skojjt-status-header button {
            background: none;
            border: none;
            color: #cdd6f4;
            cursor: pointer;
            font-size: 16px;
            padding: 0 4px;
        }
        #skojjt-status-text {
            padding: 8px 14px;
            color: #a6e3a1;
            font-weight: 500;
        }
        #skojjt-log {
            max-height: 250px;
            overflow-y: auto;
            padding: 4px 14px;
            font-family: 'Cascadia Code', 'Fira Code', monospace;
            font-size: 11px;
            line-height: 1.5;
        }
        .skojjt-log-info { color: #94e2d5; }
        .skojjt-log-warn { color: #f9e2af; }
        .skojjt-log-error { color: #f38ba8; }
        #skojjt-actions {
            display: flex;
            gap: 8px;
            padding: 10px 14px;
            border-top: 1px solid #45475a;
        }
        .skojjt-btn {
            flex: 1;
            padding: 7px 12px;
            border: none;
            border-radius: 6px;
            font-size: 13px;
            font-weight: 500;
            cursor: pointer;
            background: #89b4fa;
            color: #1e1e2e;
        }
        .skojjt-btn:hover { background: #74c7ec; }
        .skojjt-btn-secondary {
            background: #45475a;
            color: #cdd6f4;
        }
        .skojjt-btn-secondary:hover { background: #585b70; }

        /* Config panel */
        #skojjt-config {
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            width: 420px;
            background: #1e1e2e;
            color: #cdd6f4;
            border-radius: 10px;
            box-shadow: 0 8px 32px rgba(0,0,0,0.5);
            z-index: 100000;
            font-family: 'Segoe UI', system-ui, sans-serif;
            font-size: 13px;
        }
        #skojjt-config-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 12px 16px;
            background: #313244;
            border-radius: 10px 10px 0 0;
            font-weight: 600;
        }
        #skojjt-config-header button {
            background: none;
            border: none;
            color: #cdd6f4;
            cursor: pointer;
            font-size: 16px;
        }
        .skojjt-config-body {
            padding: 16px;
        }
        .skojjt-config-body label {
            display: block;
            margin-bottom: 12px;
            font-weight: 500;
            color: #bac2de;
        }
        .skojjt-config-body input,
        .skojjt-config-body select {
            display: block;
            width: 100%;
            margin-top: 4px;
            padding: 8px 10px;
            background: #313244;
            border: 1px solid #45475a;
            border-radius: 6px;
            color: #cdd6f4;
            font-size: 13px;
            box-sizing: border-box;
        }
        .skojjt-config-body input:focus,
        .skojjt-config-body select:focus {
            outline: none;
            border-color: #89b4fa;
        }
        .skojjt-config-body hr {
            border: none;
            border-top: 1px solid #45475a;
            margin: 16px 0;
        }
        .skojjt-config-actions {
            display: flex;
            gap: 8px;
            margin-top: 16px;
        }
        #cfg-status {
            margin-top: 10px;
            font-size: 12px;
            min-height: 18px;
        }
    `);

    // =========================================================================
    // Initialization
    // =========================================================================
    async function init() {
        console.log('[Skojjt] Tampermonkey script loaded (v0.4.0 — Sensus sync)');

        // Register menu commands
        GM_registerMenuCommand('⚙ Skojjt-inställningar', showConfigPanel);
        GM_registerMenuCommand('▶ Synka närvaro', startSync);

        // Create the status overlay
        createStatusOverlay();

        // Check if we're on the Sensus login page
        if (isLoginPage()) {
            log('Inloggningssida detekterad');
            setStatus('Inloggningssida');
            attemptAutoLogin();
            return;
        }

        // Check Skojjt auth status
        setStatus('Kontrollerar Skojjt-inloggning...');
        try {
            const me = await fetchMe();
            cachedMe = me;
            log(`Inloggad i Skojjt som ${me.displayName}`);
            log(`Tillgång till: ${me.accessibleGroups.map(g => g.name).join(', ')}`);
            updateReadyStatus();
        } catch (err) {
            if (err.message === 'NOT_AUTHENTICATED') {
                log('Inte inloggad i Skojjt. Logga in via inställningar.', 'warn');
                setStatus('Inte inloggad i Skojjt');
            } else {
                log(`Kunde inte nå Skojjt: ${err.message}`, 'warn');
                setStatus('Kunde inte nå Skojjt');
            }
        }
    }

    // Run after DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();