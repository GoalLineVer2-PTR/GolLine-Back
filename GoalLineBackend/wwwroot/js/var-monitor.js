'use strict';

(() => {
    const wsProto = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const WS_URL = `${wsProto}//${window.location.host}/ws/var-monitor`;
    const API_DISPAROS = '/api/Disparos';
    const GOAL_HALF_WIDTH = 3.66;
    const BALL_RADIUS = 0.11;

    // Estado UI
    let ws = null;
    let godotConnected = false;
    let expandedSlot = 0;
    let isSingleMode = false;
    let historyShots = [];
    let latestTelemetry = null;
    let isScrubbing = false;

    // Medición de FPS recibidos
    let framesInWindow = 0;
    let lastFpsTick = performance.now();

    // Referencias DOM
    const dotGodot = document.getElementById('dotGodot');
    const txtGodotStatus = document.getElementById('txtGodotStatus');
    const dotBackend = document.getElementById('dotBackend');
    const txtBackendStatus = document.getElementById('txtBackendStatus');
    const txtStreamFps = document.getElementById('txtStreamFps');

    const cameraGrid = document.getElementById('cameraGrid');
    const camCards = Array.from(document.querySelectorAll('.cam-card'));
    const camCanvases = [0, 1, 2, 3].map(i => document.getElementById(`camCanvas${i}`));
    const camCtxs = camCanvases.map(c => c.getContext('2d'));
    const camPlaceholders = [0, 1, 2, 3].map(i => document.getElementById(`camPlaceholder${i}`));

    const camTag1 = document.getElementById('camTag1');
    const camTag3 = document.getElementById('camTag3');
    const btnLineLeft = document.getElementById('btnLineLeft');
    const btnLineRight = document.getElementById('btnLineRight');
    const btnNetBehind = document.getElementById('btnNetBehind');
    const btnNetGk = document.getElementById('btnNetGk');

    const btnViewQuad = document.getElementById('btnViewQuad');
    const btnViewSingle = document.getElementById('btnViewSingle');

    const replayStatePill = document.getElementById('replayStatePill');
    const replayScrubber = document.getElementById('replayScrubber');
    const replayReadout = document.getElementById('replayReadout');
    const btnReplayPlayPause = document.getElementById('btnReplayPlayPause');
    const btnStepBack = document.getElementById('btnStepBack');
    const btnStepFwd = document.getElementById('btnStepFwd');
    const btnCriticalFrame = document.getElementById('btnCriticalFrame');
    const btnExitReplay = document.getElementById('btnExitReplay');
    const btnToggleLaser = document.getElementById('btnToggleLaser');
    const btnToggleHawkeye = document.getElementById('btnToggleHawkeye');
    const speedBtns = Array.from(document.querySelectorAll('.spd-btn'));

    const scoreboardMain = document.getElementById('scoreboardMain');
    const matchClock = document.getElementById('matchClock');
    const smartwatchBox = document.getElementById('smartwatchBox');
    const watchStateText = document.getElementById('watchStateText');
    const watchOutcomeText = document.getElementById('watchOutcomeText');
    const gkBadge = document.getElementById('gkBadge');

    const metricSpeed = document.getElementById('metricSpeed');
    const metricRearEdge = document.getElementById('metricRearEdge');
    const metricPct = document.getElementById('metricPct');
    const metricPos = document.getElementById('metricPos');
    const gltProgressFill = document.getElementById('gltProgressFill');

    const hawkeyeCanvas = document.getElementById('hawkeyeCanvas');
    const hawkeyeCtx = hawkeyeCanvas.getContext('2d');

    const historyList = document.getElementById('historyList');
    const kpiTotal = document.getElementById('kpiTotal');
    const kpiGoals = document.getElementById('kpiGoals');

    // ── Envío de comandos hacia Godot 4 vía Backend ─────────────────────
    function sendCommand(payload) {
        if (ws && ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify(payload));
        }
    }

    // ── Conexión WebSocket /ws/var-monitor ──────────────────────────────
    function connectWebSocket() {
        dotBackend.className = 'status-dot';
        txtBackendStatus.textContent = 'WS: Conectando...';

        ws = new WebSocket(WS_URL);
        ws.binaryType = 'arraybuffer';

        ws.onopen = () => {
            dotBackend.className = 'status-dot online';
            txtBackendStatus.textContent = 'WS: En Línea';
        };

        ws.onclose = () => {
            dotBackend.className = 'status-dot offline';
            txtBackendStatus.textContent = 'WS: Reconectando';
            setGodotStatus(false);
            setTimeout(connectWebSocket, 2000);
        };

        ws.onerror = () => {
            ws.close();
        };

        ws.onmessage = (event) => {
            if (event.data instanceof ArrayBuffer) {
                handleBinaryFrame(event.data);
            } else if (typeof event.data === 'string') {
                handleJsonMessage(event.data);
            }
        };
    }

    function setGodotStatus(connected) {
        godotConnected = connected;
        dotGodot.className = `status-dot ${connected ? 'online' : 'offline'}`;
        txtGodotStatus.textContent = connected ? 'Godot 4: LIVE' : 'Godot 4: Sin señal';
        if (!connected) {
            camPlaceholders.forEach(p => p.classList.remove('hidden'));
            txtStreamFps.textContent = '0.0 FPS';
        }
    }

    function handleBinaryFrame(arrayBuffer) {
        const bytes = new Uint8Array(arrayBuffer);
        if (bytes.length < 2) return;
        const slotIdx = bytes[0];
        if (slotIdx > 3) return;

        if (!godotConnected) setGodotStatus(true);
        framesInWindow++;

        const blob = new Blob([bytes.subarray(1)], { type: 'image/jpeg' });
        createImageBitmap(blob).then((bitmap) => {
            const ctx = camCtxs[slotIdx];
            const canvas = camCanvases[slotIdx];
            ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
            bitmap.close();
            camPlaceholders[slotIdx].classList.add('hidden');
        }).catch(() => {});
    }

    function handleJsonMessage(raw) {
        let msg;
        try {
            msg = JSON.parse(raw);
        } catch {
            return;
        }

        if (msg.type === 'status') {
            setGodotStatus(Boolean(msg.godotConnected));
            return;
        }

        if (msg.type === 'telemetry') {
            if (!godotConnected) setGodotStatus(true);
            latestTelemetry = msg;
            renderTelemetry(msg);
        }
    }

    // ── Renderizado de Telemetría GLT & Replay ──────────────────────────
    function renderTelemetry(t) {
        const ball = t.ball || {};
        const gk = t.goalkeeper || {};
        const match = t.match || {};
        const replay = t.replay || {};
        const overlays = t.overlays || {};

        // Marcador y reloj
        scoreboardMain.textContent = `${match.kickerTeam || 'LOC'} ${match.scoreGoals ?? 0} — ${match.scoreSaves ?? 0} ${match.gkTeam || 'VIS'}`;
        const secTotal = Number(match.matchSeconds || 2700);
        const mins = Math.floor(secTotal / 60);
        const secs = secTotal % 60;
        matchClock.textContent = `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;

        // Smartwatch & borde de cámaras
        const wState = match.watchState || 'idle';
        smartwatchBox.className = `smartwatch smartwatch--${wState}`;
        if (wState === 'gol') {
            watchStateText.textContent = '¡GOL CONFIRMADO!';
        } else if (wState === 'nogol') {
            watchStateText.textContent = 'NO ES GOL';
        } else {
            watchStateText.textContent = 'ESPERANDO';
        }
        watchOutcomeText.textContent = match.outcome || 'SENSOR ACTIVO 240Hz';

        camCards.forEach(c => {
            c.classList.toggle('state-gol', wState === 'gol');
            c.classList.toggle('state-nogol', wState === 'nogol');
        });

        // Arquero
        gkBadge.textContent = `ARQ: ${String(gk.difficulty || 'normal').toUpperCase()}`;

        // Métricas GLT
        const vel = Number(ball.velKmh || 0);
        const rearCm = Number(ball.rearEdgeCm || -1111);
        const pct = Math.max(0, Math.min(100, Number(ball.pct || 0)));
        const bx = Number(ball.x || 0);
        const by = Number(ball.y || BALL_RADIUS);
        const bz = Number(ball.z || 11.0);

        metricSpeed.textContent = `${vel.toFixed(1)} km/h`;
        metricRearEdge.textContent = `${rearCm >= 0 ? '+' : ''}${rearCm.toFixed(1)} cm`;
        metricPct.textContent = `${pct.toFixed(1)}%`;
        metricPos.textContent = `${bx.toFixed(2)}, ${by.toFixed(2)}, ${bz.toFixed(2)}`;
        gltProgressFill.style.width = `${pct}%`;

        // Esquema 2D Hawk-Eye
        drawHawkeye2D(bx, bz, pct, wState === 'gol');

        // Etiquetas dinámicas de cámaras conmutables
        const slotCams = Array.isArray(t.slotCams) ? t.slotCams : [];
        if (slotCams[1]) {
            const isLeft = slotCams[1] === 'varLeft';
            camTag1.innerHTML = `<span class="live-dot"></span> CAM 2 • LÍNEA DE GOL (${isLeft ? 'IZQ' : 'DER'})`;
            btnLineLeft.classList.toggle('active', isLeft);
            btnLineRight.classList.toggle('active', !isLeft);
        }
        if (slotCams[3]) {
            const isBehind = slotCams[3] === 'behindNet';
            camTag3.innerHTML = `<span class="live-dot"></span> CAM 4 • ${isBehind ? 'DETRÁS DEL ARCO' : 'POV ARQUERO'}`;
            btnNetBehind.classList.toggle('active', isBehind);
            btnNetGk.classList.toggle('active', !isBehind);
        }

        // Overlays
        btnToggleLaser.classList.toggle('active', Boolean(overlays.laser));
        btnToggleHawkeye.classList.toggle('active', Boolean(overlays.hawkeye));

        // Consola de Replay
        const hasReplay = Boolean(replay.available) && Number(replay.totalFrames || 0) > 1;
        const inReplay = t.mode === 'replay';
        const totalFrames = Math.max(1, Number(replay.totalFrames || 1) - 1);
        const curFrame = Number(replay.index || 0);

        replayStatePill.textContent = inReplay ? '● REPLAY VAR 3D' : 'EN VIVO';
        replayStatePill.classList.toggle('is-replay', inReplay);

        replayScrubber.disabled = !hasReplay;
        btnReplayPlayPause.disabled = !hasReplay;
        btnStepBack.disabled = !hasReplay;
        btnStepFwd.disabled = !hasReplay;
        btnCriticalFrame.disabled = !hasReplay;
        btnExitReplay.disabled = !inReplay;

        replayScrubber.max = String(totalFrames);
        if (!isScrubbing) {
            replayScrubber.value = String(curFrame);
        }
        replayReadout.textContent = `F: ${Math.round(curFrame)}/${totalFrames} • GLT: ${pct.toFixed(1)}%`;
        btnReplayPlayPause.textContent = (inReplay && replay.playing) ? '⏸ Pausar' : '▶ Reproducir';

        const curSpeed = Number(replay.speed || 0.25);
        speedBtns.forEach(b => {
            b.classList.toggle('active', Math.abs(Number(b.dataset.speed) - curSpeed) < 0.02);
        });
    }

    // ── Diagrama 2D Hawk-Eye (Vista Cenital de Línea Z=0.0) ─────────────
    function drawHawkeye2D(ballX, ballZ, pct, isGoal) {
        const w = hawkeyeCanvas.width;
        const h = hawkeyeCanvas.height;
        const ctx = hawkeyeCtx;

        ctx.clearRect(0, 0, w, h);

        // Césped
        ctx.fillStyle = '#072219';
        ctx.fillRect(0, 0, w, h);

        // Zona interior del arco (Z < 0)
        const lineY = h * 0.45;
        ctx.fillStyle = isGoal ? 'rgba(16, 185, 129, 0.22)' : 'rgba(56, 189, 248, 0.08)';
        ctx.fillRect(0, 0, w, lineY);

        // Línea de gol reglamentaria (Z = 0.0)
        ctx.fillStyle = '#f8fafc';
        ctx.fillRect(24, lineY - 2, w - 48, 4);

        // Postes
        const leftPostX = 36;
        const rightPostX = w - 36;
        ctx.fillStyle = '#e2e8f0';
        ctx.beginPath();
        ctx.arc(leftPostX, lineY, 5, 0, Math.PI * 2);
        ctx.arc(rightPostX, lineY, 5, 0, Math.PI * 2);
        ctx.fill();

        // Escala X: [-3.66, +3.66] -> [leftPostX, rightPostX]
        const normX = (ballX + GOAL_HALF_WIDTH) / (GOAL_HALF_WIDTH * 2);
        const px = leftPostX + Math.max(-0.08, Math.min(1.08, normX)) * (rightPostX - leftPostX);

        // Escala Z: zoom milimétrico cerca de la línea (-1.2m a +1.6m)
        const clampedZ = Math.max(-1.1, Math.min(1.5, ballZ));
        const pxPerMeterZ = 34;
        const py = lineY + clampedZ * pxPerMeterZ;
        const rPx = Math.max(6, BALL_RADIUS * pxPerMeterZ * 2.2);

        // Sombra y balón
        ctx.beginPath();
        ctx.arc(px, py, rPx, 0, Math.PI * 2);
        ctx.fillStyle = isGoal ? '#10b981' : (pct > 0 ? '#f59e0b' : '#38bdf8');
        ctx.fill();
        ctx.lineWidth = 2;
        ctx.strokeStyle = '#ffffff';
        ctx.stroke();

        // Etiquetas
        ctx.fillStyle = '#94a3b8';
        ctx.font = '700 10px "JetBrains Mono", monospace';
        ctx.fillText('GOL (Z < 0)', 8, 14);
        ctx.fillText('LÍNEA Z=0.00m', w - 92, lineY - 7);
        ctx.fillText('CAMPO (Z > 0)', 8, h - 7);
    }

    // ── Historial MongoDB + SignalR ─────────────────────────────────────
    async function fetchHistory() {
        try {
            const res = await fetch(API_DISPAROS, { cache: 'no-store' });
            if (!res.ok) return;
            historyShots = await res.json();
            renderHistory();
        } catch {
            historyList.innerHTML = '<div class="history-empty">No se pudo leer el historial de MongoDB.</div>';
        }
    }

    function renderHistory() {
        const total = historyShots.length;
        const goals = historyShots.filter(s => s.fueGol).length;
        kpiTotal.textContent = `${total} tiros`;
        kpiGoals.textContent = `${goals} goles`;

        if (total === 0) {
            historyList.innerHTML = '<div class="history-empty">Todavía no hay disparos registrados en MongoDB.</div>';
            return;
        }

        historyList.innerHTML = '';
        historyShots.slice(0, 25).forEach((shot, idx) => {
            const item = document.createElement('div');
            item.className = `history-item ${shot.fueGol ? 'history-item--gol' : ''}`;

            const vel = Number(shot.velocidad || 0).toFixed(1);
            const pct = Number(shot.porcentajeCruzoLinea ?? (shot.fueGol ? 100 : 0)).toFixed(0);
            const detail = shot.resultadoDetalle || (shot.fueGol ? 'GOL' : 'NO GOL');
            const origen = shot.origen || 'Godot4';

            item.innerHTML = `
                <div class="history-item__info">
                    <div class="history-item__title">${shot.fueGol ? '⚽' : '🧤'} ${detail}</div>
                    <div class="history-item__meta">${vel} km/h • GLT ${pct}% • ${origen}</div>
                </div>
                <button class="btn-load-shot" data-idx="${idx}">▶ Replay</button>
            `;

            item.querySelector('.btn-load-shot').addEventListener('click', () => {
                sendCommand({ action: 'load_history_shot', shot });
            });

            historyList.appendChild(item);
        });
    }

    function initSignalR() {
        if (!window.signalR) return;
        const connection = new window.signalR.HubConnectionBuilder()
            .withUrl('/goalHub')
            .withAutomaticReconnect()
            .build();

        connection.on('NuevoDisparo', (disparo) => {
            historyShots.unshift(disparo);
            if (historyShots.length > 50) historyShots.pop();
            renderHistory();
        });

        connection.start().catch(() => {});
    }

    // ── Controles de Layout y Cámaras ───────────────────────────────────
    function setLayoutMode(single, slotIdx = expandedSlot) {
        isSingleMode = single;
        expandedSlot = slotIdx;
        cameraGrid.classList.toggle('camera-grid--single', single);
        cameraGrid.classList.toggle('camera-grid--quad', !single);
        btnViewSingle.classList.toggle('active', single);
        btnViewQuad.classList.toggle('active', !single);
        camCards.forEach((card, i) => {
            card.classList.toggle('is-expanded', single && i === expandedSlot);
        });
    }

    btnViewQuad.addEventListener('click', () => setLayoutMode(false));
    btnViewSingle.addEventListener('click', () => setLayoutMode(true, expandedSlot));

    document.querySelectorAll('.btn-expand').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const slot = Number(btn.dataset.slot);
            if (isSingleMode && expandedSlot === slot) {
                setLayoutMode(false);
            } else {
                setLayoutMode(true, slot);
            }
        });
    });

    camCards.forEach((card, slot) => {
        card.querySelector('.cam-card__stage').addEventListener('click', () => {
            if (isSingleMode && expandedSlot === slot) {
                setLayoutMode(false);
            } else {
                setLayoutMode(true, slot);
            }
        });
    });

    window.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && isSingleMode) {
            setLayoutMode(false);
        }
    });

    btnLineLeft.addEventListener('click', () => sendCommand({ action: 'switch_line_cam', cam: 'varLeft' }));
    btnLineRight.addEventListener('click', () => sendCommand({ action: 'switch_line_cam', cam: 'varRight' }));
    btnNetBehind.addEventListener('click', () => sendCommand({ action: 'switch_net_cam', cam: 'behindNet' }));
    btnNetGk.addEventListener('click', () => sendCommand({ action: 'switch_net_cam', cam: 'gkPov' }));

    // ── Controles Remotos de Replay ─────────────────────────────────────
    btnReplayPlayPause.addEventListener('click', () => sendCommand({ action: 'replay_toggle' }));
    btnStepBack.addEventListener('click', () => sendCommand({ action: 'replay_step', delta: -1 }));
    btnStepFwd.addEventListener('click', () => sendCommand({ action: 'replay_step', delta: 1 }));
    btnCriticalFrame.addEventListener('click', () => sendCommand({ action: 'replay_critical' }));
    btnExitReplay.addEventListener('click', () => sendCommand({ action: 'replay_exit' }));

    btnToggleLaser.addEventListener('click', () => sendCommand({ action: 'toggle_laser' }));
    btnToggleHawkeye.addEventListener('click', () => sendCommand({ action: 'toggle_hawkeye' }));

    speedBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            sendCommand({ action: 'replay_speed', speed: Number(btn.dataset.speed) });
        });
    });

    replayScrubber.addEventListener('pointerdown', () => { isScrubbing = true; });
    replayScrubber.addEventListener('pointerup', () => { isScrubbing = false; });
    replayScrubber.addEventListener('input', () => {
        sendCommand({ action: 'replay_seek', frame: Number(replayScrubber.value) });
    });

    // ── Contador de FPS del stream ──────────────────────────────────────
    setInterval(() => {
        const now = performance.now();
        const elapsed = (now - lastFpsTick) / 1000;
        const fps = elapsed > 0 ? framesInWindow / elapsed : 0;
        txtStreamFps.textContent = `${fps.toFixed(1)} FPS`;
        framesInWindow = 0;
        lastFpsTick = now;
    }, 1000);

    // Arranque
    drawHawkeye2D(0, 11.0, 0, false);
    connectWebSocket();
    fetchHistory();
    initSignalR();
})();
