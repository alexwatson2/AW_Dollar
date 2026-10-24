(function () {
    'use strict';

    const IS_FIVEM = typeof window.GetParentResourceName === 'function';

    const DEFAULT_SETTINGS = {
        enabled: false,
        playerEsp: true,
        playerColor: '#FF0000',
        npcEsp: true,
        npcColor: '#00FF00',
        aimAssist: false,

        boxEsp: true,
        boxStyle: 'corner',
        boxFill: true,
        skeleton: true,
        headDot: true,
        tracers: true,
        tracerOrigin: 'bottom',
        names: true,
        healthBar: true,
        distance: true,
        weaponInfo: true,
        showDead: false,
        adminBlips: false,
        showStatusHud: true,
        maxDistance: 300,

        aimTargetPlayers: true,
        aimTargetNpcs: true,
        showFov: true,
        fovRadius: 120,
        fovColor: '#FFFFFF',
        aimSmooth: 4.5,
        aimBone: 'head',
        aimVisibleOnly: true,
        aimMaxDistance: 150,
        aimTargetLine: true,
    };

    let state = { ...DEFAULT_SETTINGS };

    // المنت‌های اصلی DOM
    const adminPanel = document.getElementById('admin-panel');
    const panelHeader = document.getElementById('panel-header');
    const masterBar = document.querySelector('.master-bar');
    const masterStatusText = document.getElementById('master-status-text');

    const fovSvg = document.getElementById('fov-svg');
    const fovCircle = document.getElementById('fov-circle');
    const fovCircleShadow = document.getElementById('fov-circle-shadow');
    const statusPill = document.getElementById('status-pill');
    const badgePlayer = document.getElementById('badge-player');
    const badgeNpc = document.getElementById('badge-npc');
    const badgeAim = document.getElementById('badge-aim');
    const dotPlayer = document.getElementById('dot-player');
    const dotNpc = document.getElementById('dot-npc');

    const swatchPlayer = document.getElementById('preview-swatch-player');
    const swatchNpc = document.getElementById('preview-swatch-npc');

    // لیست کلیدهای بولین
    const BOOLEAN_KEYS = [
        'enabled',
        'playerEsp',
        'npcEsp',
        'aimAssist',
        'boxEsp',
        'boxFill',
        'skeleton',
        'headDot',
        'tracers',
        'names',
        'healthBar',
        'distance',
        'weaponInfo',
        'showDead',
        'adminBlips',
        'showStatusHud',
        'aimTargetPlayers',
        'aimTargetNpcs',
        'showFov',
        'aimVisibleOnly',
        'aimTargetLine',
    ];

    // ارسال پیام به کلاینت Lua در FiveM
    function postNUI(endpoint, payload = {}) {
        if (!IS_FIVEM) return Promise.resolve({ ok: true });
        const resName = window.GetParentResourceName();
        return fetch(`https://${resName}/${endpoint}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json; charset=UTF-8' },
            body: JSON.stringify(payload),
        }).catch(() => {});
    }

    function normalizeHex(val, fallback) {
        if (typeof val !== 'string') return fallback;
        let clean = val.trim().replace(/[^0-9a-fA-F]/g, '');
        if (clean.length === 3) {
            clean = clean[0] + clean[0] + clean[1] + clean[1] + clean[2] + clean[2];
        }
        if (clean.length !== 6) return fallback;
        return '#' + clean.toUpperCase();
    }

    // به‌روزرسانی لایه HUD و دایره FOV (منطبق با محاسبات پیکسلی در Lua)
    function renderOverlay(overlayData) {
        const active = overlayData.active === true;
        const showHud = overlayData.showStatusHud === true && active;
        const showFov = overlayData.showFov === true && active;

        statusPill.classList.toggle('hidden', !showHud);
        if (showHud) {
            dotPlayer.style.background = overlayData.playerColor || '#FF0000';
            dotNpc.style.background = overlayData.npcColor || '#00FF00';
            badgePlayer.classList.toggle('off', !overlayData.playerEsp);
            badgeNpc.classList.toggle('off', !overlayData.npcEsp);
            badgeAim.classList.toggle('off', !overlayData.aimAssist);
        }

        fovSvg.classList.toggle('hidden', !showFov);
        if (showFov) {
            const rPx1080 = Number(overlayData.fovRadius) || 120;
            const scaledPx = (rPx1080 / 1080) * window.innerHeight;
            fovCircle.setAttribute('r', scaledPx.toFixed(1));
            fovCircleShadow.setAttribute('r', scaledPx.toFixed(1));
            fovCircle.style.stroke = overlayData.fovColor || '#FFFFFF';
        }
    }

    // همگام‌سازی مقادیر state روی تمام کنترل‌های فرم
    function syncUIFromState() {
        BOOLEAN_KEYS.forEach((key) => {
            const el = document.getElementById(`opt-${key}`);
            if (el) el.checked = Boolean(state[key]);
        });

        // سوییچ آینه Aim Assist در تب سوم
        const aimMirror = document.getElementById('opt-aimAssist-mirror');
        if (aimMirror) aimMirror.checked = Boolean(state.aimAssist);

        // وضعیت نوار اصلی بالا
        masterBar.classList.toggle('active', Boolean(state.enabled));
        masterStatusText.textContent = state.enabled
            ? 'فعال — رندر ESP و تنظیمات انتخابی در حال اجراست'
            : 'غیرفعال — برای شروع سوییچ را روشن کنید';

        // رنگ‌ها
        const pHex = normalizeHex(state.playerColor, '#FF0000');
        const nHex = normalizeHex(state.npcColor, '#00FF00');
        const fHex = normalizeHex(state.fovColor, '#FFFFFF');

        document.getElementById('opt-playerColor').value = pHex;
        document.getElementById('hex-playerColor').value = pHex;
        swatchPlayer.style.setProperty('--badge-color', pHex);

        document.getElementById('opt-npcColor').value = nHex;
        document.getElementById('hex-npcColor').value = nHex;
        swatchNpc.style.setProperty('--badge-color', nHex);

        document.getElementById('opt-fovColor').value = fHex;
        document.getElementById('hex-fovColor').value = fHex;

        // دکمه‌های چندحالته (Segmented Controls)
        setSegmentedActive('seg-boxStyle', state.boxStyle);
        setSegmentedActive('seg-tracerOrigin', state.tracerOrigin);
        setSegmentedActive('seg-aimBone', state.aimBone);

        // اسلایدرها
        setSliderUI('maxDistance', state.maxDistance, 'm');
        setSliderUI('fovRadius', state.fovRadius, 'px');
        setSliderUI('aimSmooth', Number(state.aimSmooth).toFixed(1), '');
        setSliderUI('aimMaxDistance', state.aimMaxDistance, 'm');

        // در حالت پیش‌نمایش مرورگر لایه HUD هم مستقیم آپدیت شود
        if (!IS_FIVEM) {
            renderOverlay({
                active: state.enabled,
                showStatusHud: state.enabled && state.showStatusHud,
                playerEsp: state.playerEsp,
                playerColor: pHex,
                npcEsp: state.npcEsp,
                npcColor: nHex,
                aimAssist: state.enabled && state.aimAssist,
                showFov: state.enabled && state.aimAssist && state.showFov,
                fovRadius: state.fovRadius,
                fovColor: fHex,
            });
        }
    }

    function setSegmentedActive(containerId, value) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.querySelectorAll('.seg-btn').forEach((btn) => {
            btn.classList.toggle('active', btn.dataset.val === value);
        });
    }

    function setSliderUI(key, val, suffix) {
        const input = document.getElementById(`opt-${key}`);
        const label = document.getElementById(`val-${key}`);
        if (input) input.value = val;
        if (label) label.textContent = `${val}${suffix}`;
    }

    function pushStateUpdate() {
        syncUIFromState();
        postNUI('updateSettings', state);
    }

    // ========================================================================
    // اتصال رویدادهای فرم و تب‌ها
    // ========================================================================

    // جابه‌جایی بین تب‌ها
    document.querySelectorAll('.tab-btn').forEach((btn) => {
        btn.addEventListener('click', () => {
            const targetId = btn.dataset.tab;
            document.querySelectorAll('.tab-btn').forEach((b) => b.classList.toggle('active', b === btn));
            document.querySelectorAll('.tab-pane').forEach((pane) => {
                pane.classList.toggle('active', pane.id === targetId);
            });
        });
    });

    // سوییچ‌های بولین
    BOOLEAN_KEYS.forEach((key) => {
        const el = document.getElementById(`opt-${key}`);
        if (!el) return;
        el.addEventListener('change', () => {
            state[key] = el.checked;
            pushStateUpdate();
        });
    });

    const aimMirrorEl = document.getElementById('opt-aimAssist-mirror');
    if (aimMirrorEl) {
        aimMirrorEl.addEventListener('change', () => {
            state.aimAssist = aimMirrorEl.checked;
            pushStateUpdate();
        });
    }

    // اتصال انتخابگرهای رنگ و ورودی هگز
    function bindColorPair(key, fallback) {
        const picker = document.getElementById(`opt-${key}`);
        const hexBox = document.getElementById(`hex-${key}`);

        picker.addEventListener('input', () => {
            state[key] = normalizeHex(picker.value, fallback);
            pushStateUpdate();
        });

        hexBox.addEventListener('change', () => {
            state[key] = normalizeHex(hexBox.value, state[key] || fallback);
            pushStateUpdate();
        });
    }

    bindColorPair('playerColor', '#FF0000');
    bindColorPair('npcColor', '#00FF00');
    bindColorPair('fovColor', '#FFFFFF');

    // رنگ‌های آماده (Quick Swatches)
    document.querySelectorAll('.preset-swatches').forEach((group) => {
        const targetKey = group.dataset.target;
        group.querySelectorAll('.swatch-btn').forEach((btn) => {
            btn.addEventListener('click', () => {
                state[targetKey] = btn.dataset.color;
                pushStateUpdate();
            });
        });
    });

    // دکمه‌های چندحالته
    function bindSegmented(containerId, stateKey) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.querySelectorAll('.seg-btn').forEach((btn) => {
            btn.addEventListener('click', () => {
                state[stateKey] = btn.dataset.val;
                pushStateUpdate();
            });
        });
    }

    bindSegmented('seg-boxStyle', 'boxStyle');
    bindSegmented('seg-tracerOrigin', 'tracerOrigin');
    bindSegmented('seg-aimBone', 'aimBone');

    // اسلایدرها
    function bindSlider(key) {
        const input = document.getElementById(`opt-${key}`);
        if (!input) return;
        input.addEventListener('input', () => {
            state[key] = Number(input.value);
            pushStateUpdate();
        });
    }

    bindSlider('maxDistance');
    bindSlider('fovRadius');
    bindSlider('aimSmooth');
    bindSlider('aimMaxDistance');

    // بستن و ریست
    function closeMenuUI() {
        adminPanel.classList.add('hidden');
        if (!IS_FIVEM) {
            document.getElementById('preview-reopen-btn').classList.remove('hidden');
        }
        postNUI('closeMenu');
    }

    document.getElementById('btn-close').addEventListener('click', closeMenuUI);
    document.getElementById('btn-done').addEventListener('click', closeMenuUI);

    document.getElementById('btn-reset').addEventListener('click', () => {
        if (IS_FIVEM) {
            postNUI('resetDefaults')
                .then((r) => r && r.json ? r.json() : null)
                .then((res) => {
                    if (res && res.settings) {
                        state = { ...DEFAULT_SETTINGS, ...res.settings };
                    } else {
                        const keepEnabled = state.enabled;
                        state = { ...DEFAULT_SETTINGS, enabled: keepEnabled };
                    }
                    syncUIFromState();
                });
        } else {
            const keepEnabled = state.enabled;
            state = { ...DEFAULT_SETTINGS, enabled: keepEnabled };
            syncUIFromState();
        }
    });

    window.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' || e.key === 'F10') {
            if (!adminPanel.classList.contains('hidden')) {
                e.preventDefault();
                closeMenuUI();
            } else if (!IS_FIVEM && e.key === 'F10') {
                adminPanel.classList.remove('hidden');
                document.getElementById('preview-reopen-btn').classList.add('hidden');
            }
        }
    });

    window.addEventListener('resize', () => {
        if (!fovSvg.classList.contains('hidden')) {
            const rPx1080 = Number(state.fovRadius) || 120;
            const scaledPx = (rPx1080 / 1080) * window.innerHeight;
            fovCircle.setAttribute('r', scaledPx.toFixed(1));
            fovCircleShadow.setAttribute('r', scaledPx.toFixed(1));
        }
    });

    // قابلیت درگ کردن پنل توسط ادمین
    (function enableDragging() {
        let dragging = false;
        let offsetX = 0;
        let offsetY = 0;

        panelHeader.addEventListener('mousedown', (e) => {
            if (e.target.closest('button')) return;
            const rect = adminPanel.getBoundingClientRect();
            dragging = true;
            offsetX = e.clientX - rect.left;
            offsetY = e.clientY - rect.top;
            adminPanel.style.left = `${rect.left}px`;
            adminPanel.style.top = `${rect.top}px`;
            adminPanel.style.transform = 'none';
        });

        window.addEventListener('mousemove', (e) => {
            if (!dragging) return;
            const maxX = window.innerWidth - adminPanel.offsetWidth;
            const maxY = window.innerHeight - adminPanel.offsetHeight;
            const nextX = Math.max(0, Math.min(maxX, e.clientX - offsetX));
            const nextY = Math.max(0, Math.min(maxY, e.clientY - offsetY));
            adminPanel.style.left = `${nextX}px`;
            adminPanel.style.top = `${nextY}px`;
        });

        window.addEventListener('mouseup', () => {
            dragging = false;
        });
    })();

    // ========================================================================
    // دریافت پیام‌های NUI از اسکریپت Lua در FiveM
    // ========================================================================

    window.addEventListener('message', (event) => {
        const data = event.data;
        if (!data || !data.action) return;

        if (data.action === 'openMenu') {
            if (data.settings) {
                state = { ...DEFAULT_SETTINGS, ...data.settings };
            }
            syncUIFromState();
            adminPanel.classList.remove('hidden');
        } else if (data.action === 'closeMenu') {
            adminPanel.classList.add('hidden');
        } else if (data.action === 'updateOverlay') {
            renderOverlay(data);
        }
    });

    // ========================================================================
    // شبیه‌ساز تعاملی ویژه پیش‌نمایش در مرورگر (در FiveM هرگز اجرا نمی‌شود)
    // ========================================================================
    if (!IS_FIVEM) {
        const canvas = document.getElementById('preview-canvas');
        const banner = document.getElementById('preview-banner');
        const reopenBtn = document.getElementById('preview-reopen-btn');
        canvas.classList.remove('hidden');
        banner.classList.remove('hidden');

        // در پیش‌نمایش مرورگر، منو را کمی به چپ هدایت کرده و ESP را برای مشاهده آنی روشن می‌کنیم
        state.enabled = true;
        state.aimAssist = true;
        adminPanel.classList.remove('hidden');
        adminPanel.style.left = '34px';
        adminPanel.style.top = '50%';
        adminPanel.style.transform = 'translateY(-50%)';
        syncUIFromState();

        reopenBtn.addEventListener('click', () => {
            adminPanel.classList.remove('hidden');
            reopenBtn.classList.add('hidden');
        });

        const ctx = canvas.getContext('2d');
        function resizeCanvas() {
            canvas.width = window.innerWidth;
            canvas.height = window.innerHeight;
        }
        resizeCanvas();
        window.addEventListener('resize', resizeCanvas);

        let rightMouseDown = false;
        let aimOffsetX = 0;
        let aimOffsetY = 0;
        window.addEventListener('contextmenu', (e) => e.preventDefault());
        window.addEventListener('mousedown', (e) => {
            if (e.button === 2) rightMouseDown = true;
        });
        window.addEventListener('mouseup', (e) => {
            if (e.button === 2) rightMouseDown = false;
        });

        const demoEntities = [
            { isPlayer: true, id: 12, name: '[12] Amir Hosseini (AmirX)', weapon: 'Carbine Rifle', hp: 0.92, armor: 0.75, baseX: 0.62, baseY: 0.50, h: 155, dist: 34, phase: 0.2, dead: false },
            { isPlayer: true, id: 27, name: '[27] Sara Moradi (Sara_M)', weapon: 'Combat Pistol', hp: 0.58, armor: 0.0, baseX: 0.82, baseY: 0.53, h: 125, dist: 61, phase: 2.1, dead: false },
            { isPlayer: false, id: null, name: 'NPC', weapon: 'Micro SMG', hp: 0.85, armor: 0.0, baseX: 0.72, baseY: 0.56, h: 140, dist: 45, phase: 1.1, dead: false },
            { isPlayer: false, id: null, name: 'NPC', weapon: '', hp: 1.0, armor: 0.0, baseX: 0.90, baseY: 0.49, h: 105, dist: 88, phase: 3.7, dead: false },
            { isPlayer: false, id: null, name: 'NPC [DEAD]', weapon: 'Pistol', hp: 0.0, armor: 0.0, baseX: 0.56, baseY: 0.66, h: 70, dist: 28, phase: 0.0, dead: true },
        ];

        function hexToRgba(hex, alpha) {
            const clean = normalizeHex(hex, '#FF0000').slice(1);
            const r = parseInt(clean.slice(0, 2), 16);
            const g = parseInt(clean.slice(2, 4), 16);
            const b = parseInt(clean.slice(4, 6), 16);
            return `rgba(${r}, ${g}, ${b}, ${alpha})`;
        }

        function drawSimFrame(ts) {
            const t = ts * 0.001;
            const W = canvas.width;
            const H = canvas.height;

            // پس‌زمینه شب شهر لوس‌سانتوس
            const sky = ctx.createLinearGradient(0, 0, 0, H);
            sky.addColorStop(0, '#070b14');
            sky.addColorStop(0.54, '#0f172a');
            sky.addColorStop(0.55, '#111827');
            sky.addColorStop(1, '#090d16');
            ctx.fillStyle = sky;
            ctx.fillRect(0, 0, W, H);

            // خطوط گرید خیابان برای حس عمق ۳بعدی
            ctx.strokeStyle = 'rgba(56, 189, 248, 0.06)';
            ctx.lineWidth = 1;
            const horizon = H * 0.54;
            for (let i = -8; i <= 8; i++) {
                ctx.beginPath();
                ctx.moveTo(W * 0.5 + i * 45, horizon);
                ctx.lineTo(W * 0.5 + i * 260, H);
                ctx.stroke();
            }
            for (let j = 1; j <= 6; j++) {
                const y = horizon + Math.pow(j / 6, 1.8) * (H - horizon);
                ctx.beginPath();
                ctx.moveTo(0, y);
                ctx.lineTo(W, y);
                ctx.stroke();
            }

            // نشانه وسط تصویر (Crosshair)
            const cxScreen = W * 0.5;
            const cyScreen = H * 0.5;
            ctx.strokeStyle = 'rgba(255,255,255,0.6)';
            ctx.lineWidth = 1.4;
            ctx.beginPath();
            ctx.moveTo(cxScreen - 7, cyScreen);
            ctx.lineTo(cxScreen + 7, cyScreen);
            ctx.moveTo(cxScreen, cyScreen - 7);
            ctx.lineTo(cxScreen, cyScreen + 7);
            ctx.stroke();

            if (state.enabled) {
                const fovScaled = (Number(state.fovRadius) / 1080) * H;
                let bestTarget = null;
                let bestDist = fovScaled;

                const rendered = demoEntities.map((ent) => {
                    const sway = ent.dead ? 0 : Math.sin(t * 0.9 + ent.phase) * 0.045;
                    const sx = (ent.baseX + sway) * W - aimOffsetX;
                    const sy = ent.baseY * H - aimOffsetY;
                    const bh = ent.h;
                    const bw = bh * 0.52;
                    const headY = sy - bh * 0.38;
                    const boneY =
                        state.aimBone === 'head'
                            ? headY
                            : state.aimBone === 'neck'
                            ? sy - bh * 0.28
                            : state.aimBone === 'chest'
                            ? sy - bh * 0.14
                            : sy + bh * 0.02;

                    return { ...ent, sx, sy, bw, bh, headY, boneY };
                });

                // محاسبه هدف قفل شده در Aim Assist
                if (state.aimAssist) {
                    rendered.forEach((ent) => {
                        if (ent.dead) return;
                        if (ent.isPlayer && !state.aimTargetPlayers) return;
                        if (!ent.isPlayer && !state.aimTargetNpcs) return;
                        if (ent.dist > state.aimMaxDistance) return;
                        const d = Math.hypot(ent.sx - cxScreen, ent.boneY - cyScreen);
                        if (d <= bestDist) {
                            bestDist = d;
                            bestTarget = ent;
                        }
                    });
                }

                if (rightMouseDown && bestTarget) {
                    const smooth = Math.max(1, Number(state.aimSmooth) || 4.5);
                    aimOffsetX += ((bestTarget.sx - cxScreen) / smooth) * 0.35;
                    aimOffsetY += ((bestTarget.boneY - cyScreen) / smooth) * 0.35;
                } else {
                    aimOffsetX *= 0.93;
                    aimOffsetY *= 0.93;
                }

                rendered.forEach((ent) => {
                    if (ent.isPlayer && !state.playerEsp) return;
                    if (!ent.isPlayer && !state.npcEsp) return;
                    if (ent.dead && !state.showDead) return;
                    if (ent.dist > state.maxDistance) return;

                    const hex = ent.isPlayer ? state.playerColor : state.npcColor;
                    const isLocked = bestTarget && bestTarget === ent;
                    const boxHex = isLocked ? '#FFE63C' : hex;
                    const left = ent.sx - ent.bw * 0.5;
                    const top = ent.sy - ent.bh * 0.5;

                    // ۱. خط Tracer
                    if (state.tracers) {
                        const origX = W * 0.5;
                        const origY = state.tracerOrigin === 'center' ? H * 0.5 : state.tracerOrigin === 'ped' ? H * 0.85 : H * 0.98;
                        ctx.strokeStyle = hexToRgba(hex, 0.78);
                        ctx.lineWidth = 1.4;
                        ctx.beginPath();
                        ctx.moveTo(origX, origY);
                        ctx.lineTo(ent.sx, top + ent.bh);
                        ctx.stroke();
                    }

                    // ۲. Chams Fill
                    if (state.boxFill) {
                        ctx.fillStyle = hexToRgba(hex, 0.14);
                        ctx.fillRect(left, top, ent.bw, ent.bh);
                    }

                    // ۳. اسکلت کاراکتر
                    if (state.skeleton && !ent.dead) {
                        ctx.strokeStyle = hexToRgba(hex, 0.9);
                        ctx.lineWidth = 1.5;
                        const neckY = top + ent.bh * 0.2;
                        const pelvisY = top + ent.bh * 0.54;
                        ctx.beginPath();
                        // ستون فقرات
                        ctx.moveTo(ent.sx, ent.headY);
                        ctx.lineTo(ent.sx, pelvisY);
                        // دست‌ها
                        ctx.moveTo(ent.sx - ent.bw * 0.34, top + ent.bh * 0.46);
                        ctx.lineTo(ent.sx - ent.bw * 0.22, neckY);
                        ctx.lineTo(ent.sx + ent.bw * 0.22, neckY);
                        ctx.lineTo(ent.sx + ent.bw * 0.34, top + ent.bh * 0.46);
                        // پاها
                        ctx.moveTo(ent.sx - ent.bw * 0.2, top + ent.bh * 0.95);
                        ctx.lineTo(ent.sx, pelvisY);
                        ctx.lineTo(ent.sx + ent.bw * 0.2, top + ent.bh * 0.95);
                        ctx.stroke();
                    }

                    // ۴. کادر (Corner / 2D / 3D)
                    if (state.boxEsp) {
                        ctx.strokeStyle = boxHex;
                        ctx.lineWidth = 2;
                        if (state.boxStyle === '2d') {
                            ctx.strokeRect(left, top, ent.bw, ent.bh);
                        } else if (state.boxStyle === '3d') {
                            const off = 11;
                            ctx.strokeRect(left, top, ent.bw, ent.bh);
                            ctx.strokeStyle = hexToRgba(boxHex, 0.65);
                            ctx.strokeRect(left + off, top - off, ent.bw, ent.bh);
                            ctx.beginPath();
                            ctx.moveTo(left, top); ctx.lineTo(left + off, top - off);
                            ctx.moveTo(left + ent.bw, top); ctx.lineTo(left + ent.bw + off, top - off);
                            ctx.moveTo(left, top + ent.bh); ctx.lineTo(left + off, top + ent.bh - off);
                            ctx.moveTo(left + ent.bw, top + ent.bh); ctx.lineTo(left + ent.bw + off, top + ent.bh - off);
                            ctx.stroke();
                        } else {
                            const cw = ent.bw * 0.28;
                            const ch = ent.bh * 0.25;
                            ctx.beginPath();
                            ctx.moveTo(left, top + ch); ctx.lineTo(left, top); ctx.lineTo(left + cw, top);
                            ctx.moveTo(left + ent.bw - cw, top); ctx.lineTo(left + ent.bw, top); ctx.lineTo(left + ent.bw, top + ch);
                            ctx.moveTo(left, top + ent.bh - ch); ctx.lineTo(left, top + ent.bh); ctx.lineTo(left + cw, top + ent.bh);
                            ctx.moveTo(left + ent.bw - cw, top + ent.bh); ctx.lineTo(left + ent.bw, top + ent.bh); ctx.lineTo(left + ent.bw, top + ent.bh - ch);
                            ctx.stroke();
                        }
                    }

                    // ۵. نقطه سر (Head Dot)
                    if (state.headDot) {
                        ctx.fillStyle = hex;
                        ctx.beginPath();
                        ctx.arc(ent.sx, ent.headY, 3.2, 0, Math.PI * 2);
                        ctx.fill();
                    }

                    // ۶. نوار خون و زره
                    if (state.healthBar) {
                        const hpX = left - 7;
                        ctx.fillStyle = 'rgba(0,0,0,0.75)';
                        ctx.fillRect(hpX - 1, top - 1, 5, ent.bh + 2);
                        const fillH = ent.bh * ent.hp;
                        ctx.fillStyle = ent.hp > 0.5 ? '#22c55e' : ent.hp > 0.25 ? '#eab308' : '#ef4444';
                        ctx.fillRect(hpX, top + (ent.bh - fillH), 3, fillH);

                        if (ent.armor > 0) {
                            const arX = hpX - 6;
                            const arH = ent.bh * ent.armor;
                            ctx.fillStyle = 'rgba(0,0,0,0.75)';
                            ctx.fillRect(arX - 1, top - 1, 5, ent.bh + 2);
                            ctx.fillStyle = '#38bdf8';
                            ctx.fillRect(arX, top + (ent.bh - arH), 3, arH);
                        }
                    }

                    // ۷. نام و اطلاعات سلاح/مسافت
                    ctx.font = 'bold 12px Segoe UI, sans-serif';
                    ctx.textAlign = 'center';
                    if (state.names) {
                        ctx.fillStyle = 'rgba(0,0,0,0.85)';
                        ctx.fillText(ent.name, ent.sx + 1, top - 7);
                        ctx.fillStyle = hex;
                        ctx.fillText(ent.name, ent.sx, top - 8);
                    }

                    const subParts = [];
                    if (state.distance) subParts.push(`${ent.dist}m`);
                    if (state.weaponInfo && ent.weapon) subParts.push(ent.weapon);
                    if (subParts.length > 0) {
                        const subText = subParts.join(' | ');
                        ctx.font = '600 11px Segoe UI, sans-serif';
                        ctx.fillStyle = 'rgba(0,0,0,0.85)';
                        ctx.fillText(subText, ent.sx + 1, top + ent.bh + 15);
                        ctx.fillStyle = '#e2e8f0';
                        ctx.fillText(subText, ent.sx, top + ent.bh + 14);
                    }
                });

                // خط قفل هدف Aim Assist
                if (bestTarget && state.aimAssist && state.aimTargetLine) {
                    ctx.strokeStyle = hexToRgba(state.fovColor || '#FFFFFF', 0.85);
                    ctx.lineWidth = 1.4;
                    ctx.beginPath();
                    ctx.moveTo(cxScreen, cyScreen);
                    ctx.lineTo(bestTarget.sx, bestTarget.boneY);
                    ctx.stroke();
                }
            }

            requestAnimationFrame(drawSimFrame);
        }

        requestAnimationFrame(drawSimFrame);
    }
})();
