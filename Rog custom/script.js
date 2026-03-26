document.addEventListener('DOMContentLoaded', () => {

    // ══════ FX ENGINE ══════
    const canvas = document.getElementById('fx-canvas');
    const ctx = canvas.getContext('2d');
    let particles = [];
    let currentFx = 'idle'; // idle | warp | embers | snowflakes | orbs
    let animFrame = null;

    function resizeCanvas() {
        canvas.width = window.innerWidth;
        canvas.height = window.innerHeight;
    }
    window.addEventListener('resize', resizeCanvas);
    resizeCanvas();

    // ── STAR / WARP PARTICLE ──
    class Star {
        constructor() { this.reset(); }
        reset() {
            this.x = (Math.random() - 0.5) * canvas.width * 2;
            this.y = (Math.random() - 0.5) * canvas.height * 2;
            this.z = Math.random() * 1500 + 500;
            this.pz = this.z;
        }
        update(speed) {
            this.pz = this.z;
            this.z -= speed;
            if (this.z <= 1) this.reset();
        }
        draw() {
            const cx = canvas.width / 2;
            const cy = canvas.height / 2;
            const sx = (this.x / this.z) * 400 + cx;
            const sy = (this.y / this.z) * 400 + cy;
            const px = (this.x / this.pz) * 400 + cx;
            const py = (this.y / this.pz) * 400 + cy;
            const size = Math.max(0, (1 - this.z / 2000) * 3);
            const alpha = Math.max(0, 1 - this.z / 2000);
            ctx.beginPath();
            ctx.moveTo(px, py);
            ctx.lineTo(sx, sy);
            ctx.strokeStyle = `rgba(255,255,255,${alpha})`;
            ctx.lineWidth = size;
            ctx.stroke();
            // bright tip
            ctx.beginPath();
            ctx.arc(sx, sy, size * 0.8, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(200,220,255,${alpha})`;
            ctx.fill();
        }
    }

    // ── EMBER PARTICLE (Performance mode) ──
    class Ember {
        constructor() { this.reset(); }
        reset() {
            this.x = Math.random() * canvas.width;
            this.y = canvas.height + 20;
            this.vx = (Math.random() - 0.5) * 1.5;
            this.vy = -(Math.random() * 3 + 1.5);
            this.size = Math.random() * 3 + 1;
            this.life = Math.random() * 200 + 100;
            this.maxLife = this.life;
        }
        update() {
            this.x += this.vx;
            this.y += this.vy;
            this.vx += (Math.random() - 0.5) * 0.15;
            this.life--;
            if (this.life <= 0 || this.y < -20) this.reset();
        }
        draw() {
            const alpha = this.life / this.maxLife;
            const r = 255, g = Math.floor(100 + 80 * alpha), b = 0;
            ctx.beginPath();
            ctx.arc(this.x, this.y, this.size * alpha, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(${r},${g},${b},${alpha * 0.8})`;
            ctx.fill();
            ctx.shadowBlur = 8;
            ctx.shadowColor = `rgba(255,130,0,${alpha * 0.5})`;
            ctx.fill();
            ctx.shadowBlur = 0;
        }
    }

    // ── SNOWFLAKE (Silent mode) ──
    class Snowflake {
        constructor() { this.reset(); }
        reset() {
            this.x = Math.random() * canvas.width;
            this.y = -10;
            this.vx = (Math.random() - 0.5) * 0.5;
            this.vy = Math.random() * 0.8 + 0.3;
            this.size = Math.random() * 2.5 + 0.5;
            this.wobbleOffset = Math.random() * Math.PI * 2;
        }
        update(t) {
            this.x += this.vx + Math.sin(t * 0.001 + this.wobbleOffset) * 0.3;
            this.y += this.vy;
            if (this.y > canvas.height + 10) this.reset();
        }
        draw() {
            const alpha = 0.3 + this.size / 3 * 0.4;
            ctx.beginPath();
            ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(150,200,255,${alpha})`;
            ctx.fill();
        }
    }

    // ── FLOATING ORB (Balanced/Windows) ──
    class Orb {
        constructor() { this.reset(); }
        reset() {
            this.x = Math.random() * canvas.width;
            this.y = Math.random() * canvas.height;
            this.vx = (Math.random() - 0.5) * 0.3;
            this.vy = (Math.random() - 0.5) * 0.3;
            this.size = Math.random() * 60 + 30;
            this.hue = Math.random() * 60 + 240; // purple range
        }
        update() {
            this.x += this.vx;
            this.y += this.vy;
            if (this.x < -100 || this.x > canvas.width + 100) this.vx *= -1;
            if (this.y < -100 || this.y > canvas.height + 100) this.vy *= -1;
        }
        draw() {
            const grad = ctx.createRadialGradient(this.x, this.y, 0, this.x, this.y, this.size);
            grad.addColorStop(0, `hsla(${this.hue}, 80%, 60%, 0.08)`);
            grad.addColorStop(1, `hsla(${this.hue}, 80%, 60%, 0)`);
            ctx.beginPath();
            ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
            ctx.fillStyle = grad;
            ctx.fill();
        }
    }

    function switchFx(mode) {
        particles = [];
        currentFx = mode;
        document.body.classList.toggle('fx-active', mode !== 'idle');

        if (mode === 'warp') {
            for (let i = 0; i < 600; i++) particles.push(new Star());
        } else if (mode === 'embers') {
            for (let i = 0; i < 120; i++) particles.push(new Ember());
        } else if (mode === 'snowflakes') {
            for (let i = 0; i < 100; i++) particles.push(new Snowflake());
        } else if (mode === 'orbs') {
            for (let i = 0; i < 12; i++) particles.push(new Orb());
        }
    }

    let warpSpeed = 3;
    let targetWarpSpeed = 3;

    function animateFx(t) {
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Smooth speed transition on warp
        warpSpeed += (targetWarpSpeed - warpSpeed) * 0.02;

        if (currentFx === 'warp') {
            particles.forEach(p => { p.update(warpSpeed); p.draw(); });
        } else if (currentFx === 'embers') {
            particles.forEach(p => { p.update(); p.draw(); });
        } else if (currentFx === 'snowflakes') {
            particles.forEach(p => { p.update(t); p.draw(); });
        } else if (currentFx === 'orbs') {
            particles.forEach(p => { p.update(); p.draw(); });
        }

        animFrame = requestAnimationFrame(animateFx);
    }
    animFrame = requestAnimationFrame(animateFx);

    // ══════ MODE MAPPING ══════
    const modeEffectMap = {
        'Silent': 'snowflakes',
        'Windows': 'orbs',
        'Balanced': 'orbs',
        'Performance': 'embers',
        'Turbo': 'warp',
    };

    const modeMessages = {
        'Silent': '❄️  SILENT MODE — CPU Turbo disabled, fans lowered',
        'Windows': '🖥️  WINDOWS MODE — System defaults restored',
        'Balanced': '⚖️  BALANCED MODE — Everyday performance',
        'Performance': '⚡  PERFORMANCE MODE — High Performance plan active',
        'Turbo': '🚀  TURBO ENGAGED — GPU power limits maxed, warp speed!',
    };

    // ══════ NAV ══════
    document.querySelectorAll('.nav-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.section').forEach(s => s.classList.remove('active'));
            btn.classList.add('active');
            document.getElementById(`section-${btn.dataset.section}`).classList.add('active');
        });
    });

    // ══════ MODE SWITCHING ══════
    document.querySelectorAll('.mode-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const mode = btn.dataset.mode;

            // ── INSTANT visual update FIRST (no await) ──
            document.querySelectorAll('.mode-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            document.body.className = `mode-${mode.toLowerCase()} fx-active`;

            // Switch particle FX immediately
            switchFx(modeEffectMap[mode] || 'idle');

            // Turbo warp acceleration
            if (mode === 'Turbo') {
                targetWarpSpeed = 35;
                setTimeout(() => { targetWarpSpeed = 12; }, 3000);
            } else {
                targetWarpSpeed = 3;
            }

            // Show status toast immediately
            showModeStatus(modeMessages[mode] || mode);

            // ── THEN send to C# backend silently in background ──
            if (window.chrome?.webview?.hostObjects) {
                window.chrome.webview.hostObjects.backend.SetMode(mode)
                    .then(() => {
                        // After mode change, refresh OC sliders to show new power limits
                        setTimeout(loadOcInfo, 500);
                    })
                    .catch(() => showError('Backend communication failed'));
            }
        });
    });

    function showModeStatus(msg) {
        const el = document.getElementById('modeStatus');
        el.textContent = msg;
        el.classList.add('show');
        setTimeout(() => el.classList.remove('show'), 3500);
    }

    function showError(msg) {
        const t = document.getElementById('errorToast');
        t.textContent = msg;
        t.classList.add('show');
        setTimeout(() => t.classList.remove('show'), 3000);
    }

    // ══════ THERMAL SAFETY ALERTS ══════
    let lastAlertTime = 0;
    function checkThermalSafety(s) {
        const now = Date.now();
        if (now - lastAlertTime < 10000) return; // 10s cooldown
        
        let msg = null;
        let isCritical = false;
        
        if (s.gpuTemp >= 88) { msg = `🔥 CRITICAL GPU THERMALS: ${Math.round(s.gpuTemp)}°C! Throttle probable.`; isCritical = true;}
        else if (s.cpuTemp >= 95) { msg = `🔥 CRITICAL CPU THERMALS: ${Math.round(s.cpuTemp)}°C! Throttle probable.`; isCritical = true;}
        else if (s.gpuTemp >= 84 && s.gpuUsage > 95) { msg = `⚠️ High GPU Thermals: Approaching throttle limit.`; }
        else if (s.ramUsage >= 95) { msg = `⚠️ CRITICAL MEMORY: System RAM usage >95%.`; isCritical = true;}
        else if (s.vramUsage >= 98) { msg = `⚠️ VRAM LIMIT: GPU Memory is full. Expect stuttering.`; }
        
        if (msg) {
            const t = document.getElementById('safetyToast');
            t.textContent = msg;
            t.style.background = isCritical ? 'rgba(255,10,30,0.95)' : 'rgba(255,136,0,0.9)';
            t.style.borderLeftColor = isCritical ? '#fff' : '#000';
            t.style.color = isCritical ? '#fff' : '#222';
            t.classList.add('show');
            setTimeout(() => t.classList.remove('show'), 5000);
            lastAlertTime = now;
        }
    }

    // ══════ LIVE PERFORMANCE TELEMETRY ══════
    const historyMax = 60; // 60 data points (~48s history)
    const statsHistory = [];
    
    function drawLiveGraph() {
        const liveCanvas = document.getElementById('liveStatsCanvas');
        if (!liveCanvas) return;
        
        // Auto-resize once on first draw
        if (liveCanvas.width === 300) { 
            const rect = liveCanvas.parentElement.getBoundingClientRect();
            if(rect.width > 0) { liveCanvas.width = rect.width; liveCanvas.height = rect.height; }
        }

        const liveCtx = liveCanvas.getContext('2d');
        if (statsHistory.length === 0) return;
        
        const w = liveCanvas.width;
        const h = liveCanvas.height;
        liveCtx.clearRect(0, 0, w, h);
        
        // Grid
        liveCtx.strokeStyle = 'rgba(255,255,255,0.05)';
        liveCtx.lineWidth = 1;
        liveCtx.beginPath();
        for(let i=1; i<4; i++) {
            let y = h * (i/4);
            liveCtx.moveTo(0, y);
            liveCtx.lineTo(w, y);
        }
        liveCtx.stroke();

        const pX = (idx) => (idx / (historyMax - 1)) * w;
        const pY = (val, max) => h - ((Math.min(val, max) / max) * h) * 0.95;

        const drawLine = (key, color, maxVal) => {
            liveCtx.strokeStyle = color;
            liveCtx.lineWidth = 2;
            liveCtx.lineJoin = 'round';
            liveCtx.beginPath();
            for (let i = 0; i < statsHistory.length; i++) {
                const val = statsHistory[i][key] || 0;
                const x = pX(i + (historyMax - statsHistory.length));
                const y = pY(val, maxVal);
                if (i === 0) liveCtx.moveTo(x, y);
                else liveCtx.lineTo(x, y);
            }
            liveCtx.stroke();
        };

        drawLine('cpuTemp', '#ff4444', 100);
        drawLine('gpuTemp', '#44ff88', 100);
        drawLine('cpuUsage', '#ff8800', 100);
        drawLine('gpuUsage', '#00d4ff', 100);
    }

    // ══════ HARDWARE STATS POLLING ══════
    async function pollStats() {
        if (!window.chrome?.webview?.hostObjects) return;
        try {
            const raw = await window.chrome.webview.hostObjects.backend.GetStats();
            const s = JSON.parse(raw);

            document.getElementById('cpuTemp').textContent = Math.round(s.cpuTemp || 0);
            document.getElementById('cpuUsage').textContent = Math.round(s.cpuUsage || 0);
            document.getElementById('cpuUsageBar').style.width = `${s.cpuUsage || 0}%`;
            document.getElementById('cpuPower').textContent = (s.cpuPower || 0).toFixed(1);
            document.getElementById('cpuPowerBar').style.width = `${Math.min((s.cpuPower / 100) * 100, 100)}%`;
            document.getElementById('cpuFan').textContent = s.cpuFan || '-- RPM';

            document.getElementById('gpuTemp').textContent = Math.round(s.gpuTemp || 0);
            document.getElementById('gpuUsage').textContent = Math.round(s.gpuUsage || 0);
            document.getElementById('gpuUsageBar').style.width = `${s.gpuUsage || 0}%`;
            document.getElementById('gpuPower').textContent = (s.gpuPower || 0).toFixed(1);
            document.getElementById('gpuPowerBar').style.width = `${Math.min((s.gpuPower / 200) * 100, 100)}%`;
            document.getElementById('gpuVram').textContent = Math.round(s.vramUsage || 0);
            document.getElementById('gpuVramBar').style.width = `${s.vramUsage || 0}%`;
            document.getElementById('gpuFan').textContent = s.gpuFan || '-- RPM';

            document.getElementById('ramUsage').textContent = Math.round(s.ramUsage || 0);
            document.getElementById('ramUsageBar').style.width = `${s.ramUsage || 0}%`;

            if (s.gpuName) document.getElementById('gpuNameDisplay').textContent = s.gpuName;
            if (s.lastError) showError(s.lastError);

            // Sync mode from backend
            if (s.currentMode) {
                const activeBtn = document.querySelector(`.mode-btn[data-mode="${s.currentMode}"]`);
                if (activeBtn && !activeBtn.classList.contains('active')) {
                    document.querySelectorAll('.mode-btn').forEach(b => b.classList.remove('active'));
                    activeBtn.classList.add('active');
                    document.body.className = `mode-${s.currentMode.toLowerCase()} fx-active`;
                    switchFx(modeEffectMap[s.currentMode] || 'idle');
                }
            }

            // Sync Performance Graph
            statsHistory.push(s);
            if (statsHistory.length > historyMax) statsHistory.shift();
            drawLiveGraph();

            // Run Thermal Guard checks
            checkThermalSafety(s);

        } catch(e) {
            console.error('Stats error:', e);
        }
    }

    setInterval(pollStats, 800);

    // Start with default gentle orbs
    switchFx('orbs');

    // ══════ AI CHAT (GROQ) ══════
    const GROQ_KEY = '';
    const GROQ_MODEL = 'llama-3.3-70b-versatile';
    let chatHistory = [];
    let lastStats = null;
    let lastProcesses = null;
    let lastOcInfo = null;

    // Track latest stats for AI context
    async function pollStatsAndCache() {
        if (!window.chrome?.webview?.hostObjects) return;
        try {
            const raw = await window.chrome.webview.hostObjects.backend.GetStats();
            lastStats = JSON.parse(raw);
        } catch(_) {}
        try {
            const procRaw = await window.chrome.webview.hostObjects.backend.GetProcesses();
            lastProcesses = JSON.parse(procRaw);
        } catch(_) {}
        try {
            const ocRaw = await window.chrome.webview.hostObjects.backend.GetOcInfo();
            lastOcInfo = JSON.parse(ocRaw);
        } catch(_) {}
    }

    function buildSystemPrompt(stats, procs) {
        const s = stats;
        const statsBlock = s ? `
LIVE PC STATS (captured right now):
  CPU: ${Math.round(s.cpuTemp || 0)}°C temp, ${Math.round(s.cpuUsage || 0)}% usage, ${(s.cpuPower || 0).toFixed(1)}W power, fan @ ${s.cpuFan || 'unknown'}
  GPU: ${s.gpuName || 'unknown'} — ${Math.round(s.gpuTemp || 0)}°C, ${Math.round(s.gpuUsage || 0)}% usage, ${(s.gpuPower || 0).toFixed(1)}W draw, VRAM ${Math.round(s.vramUsage || 0)}%, fan @ ${s.gpuFan || 'unknown'}
  RAM: ${Math.round(s.ramUsage || 0)}% used
  Active mode: ${s.currentMode || 'unknown'}` : 'Live stats not available yet — use general PC knowledge.';

        let procBlock = '';
        if (procs && procs.processes) {
            const p = procs;
            procBlock = `
SYSTEM INFO:
  OS: ${p.osVersion || 'unknown'}
  CPU Cores: ${p.cpuCores || '?'}
  Machine: ${p.machineName || '?'}
  Uptime: ${p.uptime || '?'}
  Total processes running: ${p.totalProcessCount || '?'}

TOP 25 APPS BY MEMORY (like Task Manager):
${p.processes.map(proc => `  ${proc.name}${proc.title ? ' ("' + proc.title + '")' : ''} — ${proc.memMb} MB RAM (PID: ${proc.id})`).join('\n')}`;
        }

        let ocBlock = '';
        if (lastOcInfo && lastOcInfo.supported) {
            const o = lastOcInfo;
            ocBlock = `
GPU OC STATE:
  GPU: ${o.gpuName || 'unknown'}
  Current Core Clock: ${o.currentCoreMHz || '?'}MHz (max supported: ${o.maxCoreMHz || '?'}MHz)
  Current Memory Clock: ${o.currentMemMHz || '?'}MHz (max supported: ${o.maxMemMHz || '?'}MHz)
  Power Limit: ${Math.round((o.powerLimitW || o.defaultPowerW) / o.defaultPowerW * 100)}% (range: ${Math.round(o.minPowerW / o.defaultPowerW * 100)}% to ${Math.round(o.maxPowerW / o.defaultPowerW * 100)}%, where 100% is the default of ${o.defaultPowerW}W)
  HARD SAFETY CAPS: Core max offset +${o.maxCoreOcOffset || 100}MHz, Memory max offset +${o.maxMemOcOffset || 300}MHz above defaults`;
        }

        return `You are RIGAI, an expert PC performance assistant embedded inside a native Windows desktop app called ROG Custom. You can see the user's live hardware stats, running processes (like Task Manager), AND their GPU overclocking state.

${statsBlock}
${procBlock}
${ocBlock}

Guidelines:
- Be direct, practical, and concise. Use bullet points for action steps.
- Reference the actual live stats and process list above when relevant.
- When the user asks what to close, look at the process list and identify heavyweight/unnecessary apps eating RAM or CPU.
- Common RAM hogs to flag: browsers with many tabs, Discord, Spotify, Adobe apps, game launchers, Microsoft Teams.
- IMPORTANT: When recommending closing a process, include a kill tag like [CLOSE:PID:name] — for example [CLOSE:1234:Discord]. This tag will be rendered as a clickable button for the user. Include the EXACT PID from the process list.
- Suggest real, actionable Windows tips: Task Manager tricks, power plan changes, disabling startup apps, clearing RAM, Game Mode, etc.

OVERCLOCKING REGULATIONS (STRICT — FOLLOW EXACTLY):
- You may ONLY recommend GPU overclocks. You CANNOT apply them yourself.
- Always recommend CONSERVATIVE values. Start small (+25-50MHz core, +100-150MHz memory).
- NEVER recommend core clock offsets above +100MHz or memory above +300MHz. These are HARD LIMITS.
- Always check the GPU temperature first. If GPU temp > 80°C, do NOT recommend any overclock. Tell the user to cool down first.
- If GPU temp > 70°C, only recommend mild core OC (+25MHz) and NO memory OC.
- If GPU temp < 65°C, you have full headroom for conservative OC.
- When recommending an OC, output a tag like [OC:CORE:MHz] or [OC:MEM:MHz] or [OC:POWER:percentage]. Example: [OC:CORE:2100] or [OC:MEM:7500] or [OC:POWER:105]. These become Approve buttons the user must click.
- IMPORTANT: Power limits must be recommended as a PERCENTAGE (e.g. 100 for default, 110 for +10%). Look at the allowed percentage range in the stats above.
- Always explain WHY you chose those values (referencing temps, power headroom, current clocks).
- Recommend ONE step at a time. Do not suggest core+memory+power all at once. Start with core, verify stability, then memory.
- If the user asks for aggressive OC, you MUST still stay within safety limits. Warn them about risks.

UNDERVOLTING GUIDANCE:
- GPU undervolting (voltage control): nvidia-smi does NOT support this. Recommend MSI Afterburner's voltage/frequency curve editor. Lower voltage at same clock = less heat + less power + same performance.
- CPU undervolting: Recommend ThrottleStop (Intel) or Ryzen Master (AMD). Reduces heat/power without reducing speed.
- ALWAYS warn: undervolting can cause crashes/BSODs if too aggressive. Start with -25mV to -50mV increments.
- If GPU temp > 80°C or CPU temp > 85°C, proactively suggest undervolting as a safer alternative to overclocking.
- Never claim the app can undervolt directly — recommend external tools.

UNDERCLOCKING (THIS APP CAN DO THIS):
- The app CAN underclock the GPU by locking to LOWER clock speeds using the same clock locking system.
- If the user's GPU is too hot or they want quieter fans, suggest LOWER clock values using [OC:CORE:MHz] or [OC:MEM:MHz] tags.
- Example: if default core is 1900MHz and GPU is at 85°C, suggest locking to 1700MHz: [OC:CORE:1700]
- Underclocking is 100% safe — it just reduces performance slightly in exchange for much lower temps and fan noise.
- Good for laptops, quiet builds, or when gaming at lower resolutions where max clocks aren't needed.
- If temps/usage are high, flag it right away.
- Mention which performance mode they should use when relevant (Silent/Balanced/Performance/Turbo).
- Keep responses under 200 words unless the question needs more depth.
- Do NOT use markdown headers. Use emoji sparingly for bullet points.
- Sound like a knowledgeable friend, not a robot.`;
    }

    function appendMessage(role, content, isHtml = false) {
        const msgs = document.getElementById('aiMessages');
        const div = document.createElement('div');
        div.className = `ai-msg ${role}`;
        const bubble = document.createElement('div');
        bubble.className = 'msg-bubble';
        if (isHtml) {
            bubble.innerHTML = content;
        } else {
            bubble.textContent = content;
        }
        div.appendChild(bubble);
        msgs.appendChild(div);
        msgs.scrollTop = msgs.scrollHeight;
        return bubble;
    }

    // Global kill function for AI-generated buttons
    window.killProcess = async function(pid, name) {
        if (!window.chrome?.webview?.hostObjects) {
            showError('Backend offline - cannot close process');
            return;
        }
        try {
            const resultJson = await window.chrome.webview.hostObjects.backend.KillProcess(pid);
            const result = JSON.parse(resultJson);
            if (result.success) {
                appendMessage('assistant', `✅ ${result.message}`, true);
                // Disable the button that was clicked
                const btn = document.querySelector(`[data-kill-pid="${pid}"]`);
                if (btn) { btn.disabled = true; btn.textContent = '✅ Closed'; btn.style.opacity = '0.5'; }
            } else {
                appendMessage('assistant', `⚠️ ${result.message}`, true);
            }
        } catch(e) {
            showError('Failed to close process: ' + e.message);
        }
    };

    function showTyping() {
        const msgs = document.getElementById('aiMessages');
        const div = document.createElement('div');
        div.className = 'ai-msg assistant';
        div.id = 'typingIndicator';
        div.innerHTML = `<div class="msg-bubble"><div class="typing-indicator"><span></span><span></span><span></span></div></div>`;
        msgs.appendChild(div);
        msgs.scrollTop = msgs.scrollHeight;
    }

    function removeTyping() {
        document.getElementById('typingIndicator')?.remove();
    }

    async function sendToGroq(userMessage) {
        const input = document.getElementById('aiInput');
        const sendBtn = document.getElementById('aiSend');
        const scanBtn = document.getElementById('scanBtn');

        input.disabled = true;
        sendBtn.disabled = true;
        scanBtn.disabled = true;

        // Fetch latest stats if possible
        await pollStatsAndCache();

        // Add user message to history and UI
        chatHistory.push({ role: 'user', content: userMessage });
        appendMessage('user', userMessage);
        showTyping();

        try {
            const response = await fetch('https://api.groq.com/openai/v1/chat/completions', {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${GROQ_KEY}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    model: GROQ_MODEL,
                    messages: [
                        { role: 'system', content: buildSystemPrompt(lastStats, lastProcesses) },
                        ...chatHistory
                    ],
                    stream: true,
                    max_tokens: 350,
                    temperature: 0.65
                })
            });

            removeTyping();

            if (!response.ok) {
                const err = await response.text();
                appendMessage('assistant', `⚠️ API error: ${err}`);
                return;
            }

            // Stream the response token by token
            const bubble = appendMessage('assistant', '');
            let fullText = '';
            const reader = response.body.getReader();
            const decoder = new TextDecoder();

            while (true) {
                const { done, value } = await reader.read();
                if (done) break;
                const chunk = decoder.decode(value, { stream: true });
                const lines = chunk.split('\n').filter(l => l.startsWith('data: '));
                for (const line of lines) {
                    const data = line.slice(6);
                    if (data === '[DONE]') break;
                    try {
                        const json = JSON.parse(data);
                        const token = json.choices?.[0]?.delta?.content || '';
                        fullText += token;
                        bubble.textContent = fullText;
                        document.getElementById('aiMessages').scrollTop = document.getElementById('aiMessages').scrollHeight;
                    } catch(_) {}
                }
            }

            chatHistory.push({ role: 'assistant', content: fullText });

            // Convert [CLOSE:PID:name] and [OC:...] tags into clickable buttons
            let htmlified = fullText.replace(
                /\[CLOSE:(\d+):([^\]]+)\]/g,
                (_, pid, name) => `<button class="kill-btn" data-kill-pid="${pid}" onclick="killProcess(${pid}, '${name.replace(/'/g, "\\'")}')">❌ Close ${name}</button>`
            );
            htmlified = htmlified.replace(
                /\[OC:CORE:(\d+)\]/g,
                (_, mhz) => `<button class="oc-approve-btn" onclick="approveOc('core', ${mhz})">✅ Approve Core → ${mhz}MHz</button>`
            );
            htmlified = htmlified.replace(
                /\[OC:MEM:(\d+)\]/g,
                (_, mhz) => `<button class="oc-approve-btn" onclick="approveOc('mem', ${mhz})">✅ Approve Memory → ${mhz}MHz</button>`
            );
            htmlified = htmlified.replace(
                /\[OC:POWER:([\d.]+)\]/g,
                (_, pct) => `<button class="oc-approve-btn" onclick="approveOc('power', ${pct})">✅ Approve Power → ${pct}%</button>`
            );
            if (htmlified !== fullText) {
                bubble.innerHTML = htmlified;
            }
        } catch(err) {
            removeTyping();
            appendMessage('assistant', `⚠️ Couldn't reach RIGAI. Check your internet connection.\n\n${err.message}`);
        } finally {
            input.disabled = false;
            sendBtn.disabled = false;
            scanBtn.disabled = false;
            input.focus();
        }
    }

    // Wire up send button and enter key
    document.getElementById('aiSend').addEventListener('click', () => {
        const input = document.getElementById('aiInput');
        const msg = input.value.trim();
        if (!msg) return;
        input.value = '';
        sendToGroq(msg);
    });

    document.getElementById('aiInput').addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            document.getElementById('aiSend').click();
        }
    });

    // Analyse PC button — auto-sends a scan prompt
    document.getElementById('scanBtn').addEventListener('click', () => {
        sendToGroq('Analyse my PC stats right now and give me your top 3 actionable tips to improve performance or thermals.');
    });

    // Quick chip buttons
    document.querySelectorAll('.chip').forEach(chip => {
        chip.addEventListener('click', () => {
            sendToGroq(chip.dataset.prompt);
        });
    });

    // ══════ OC CONTROLS ══════
    let ocInfo = null;

    async function loadOcInfo() {
        if (!window.chrome?.webview?.hostObjects) return;
        try {
            const raw = await window.chrome.webview.hostObjects.backend.GetOcInfo();
            ocInfo = JSON.parse(raw);
            if (!ocInfo.supported) {
                document.getElementById('ocUnsupported').style.display = 'block';
                document.getElementById('ocControls').style.display = 'none';
                document.getElementById('ocScannerUi').style.display = 'none';
                return;
            }
            document.getElementById('ocScannerUi').style.display = 'block';
            document.getElementById('ocGpuName').textContent = ocInfo.gpuName || 'NVIDIA GPU';

            // Set slider ranges based on actual hardware
            const coreSlider = document.getElementById('ocCoreSlider');
            const memSlider = document.getElementById('ocMemSlider');
            const powerSlider = document.getElementById('ocPowerSlider');

            if (ocInfo.currentCoreMHz) {
                coreSlider.min = Math.max(300, ocInfo.currentCoreMHz - 200);
                coreSlider.max = ocInfo.currentCoreMHz + 100;
                coreSlider.value = ocInfo.currentCoreMHz;
                document.getElementById('ocCoreVal').textContent = ocInfo.currentCoreMHz + ' MHz';
            }
            if (ocInfo.currentMemMHz) {
                memSlider.min = Math.max(300, ocInfo.currentMemMHz - 500);
                memSlider.max = ocInfo.currentMemMHz + 300;
                memSlider.value = ocInfo.currentMemMHz;
                document.getElementById('ocMemVal').textContent = ocInfo.currentMemMHz + ' MHz';
            }
            if (ocInfo.minPowerW && ocInfo.maxPowerW && ocInfo.defaultPowerW) {
                const minPct = Math.round((ocInfo.minPowerW / ocInfo.defaultPowerW) * 100);
                const maxPct = Math.round((ocInfo.maxPowerW / ocInfo.defaultPowerW) * 100);
                const curWatts = ocInfo.powerLimitW || ocInfo.defaultPowerW;
                const curPct = Math.round((curWatts / ocInfo.defaultPowerW) * 100);

                powerSlider.min = minPct;
                powerSlider.max = maxPct;
                powerSlider.value = curPct;
                document.getElementById('ocPowerVal').textContent = curPct + ' %';
            }
        } catch(e) { console.error('OC info error:', e); }
    }

    // Load OC info on startup
    setTimeout(loadOcInfo, 2000);

    // Live slider value display
    document.getElementById('ocCoreSlider').addEventListener('input', e => {
        document.getElementById('ocCoreVal').textContent = e.target.value + ' MHz';
    });
    document.getElementById('ocMemSlider').addEventListener('input', e => {
        document.getElementById('ocMemVal').textContent = e.target.value + ' MHz';
    });
    document.getElementById('ocPowerSlider').addEventListener('input', e => {
        document.getElementById('ocPowerVal').textContent = e.target.value + ' %';
    });

    // Apply buttons
    document.getElementById('applyCoreBtn').addEventListener('click', async () => {
        if (!window.chrome?.webview?.hostObjects) return;
        const mhz = parseInt(document.getElementById('ocCoreSlider').value);
        const result = JSON.parse(await window.chrome.webview.hostObjects.backend.ApplyGpuCoreClock(mhz));
        showModeStatus(result.success ? `⚡ ${result.message}` : `⚠️ ${result.message}`);
    });

    document.getElementById('applyMemBtn').addEventListener('click', async () => {
        if (!window.chrome?.webview?.hostObjects) return;
        const mhz = parseInt(document.getElementById('ocMemSlider').value);
        const result = JSON.parse(await window.chrome.webview.hostObjects.backend.ApplyGpuMemClock(mhz));
        showModeStatus(result.success ? `⚡ ${result.message}` : `⚠️ ${result.message}`);
    });

    document.getElementById('applyPowerBtn').addEventListener('click', async () => {
        if (!window.chrome?.webview?.hostObjects || !ocInfo) return;
        const pct = parseFloat(document.getElementById('ocPowerSlider').value);
        const watts = ocInfo.defaultPowerW * (pct / 100);
        const result = JSON.parse(await window.chrome.webview.hostObjects.backend.ApplyGpuPowerLimit(watts));
        showModeStatus(result.success ? `⚡ ${result.message}` : `⚠️ ${result.message}`);
    });

    // Reset button
    document.getElementById('resetOcBtn').addEventListener('click', async () => {
        if (!window.chrome?.webview?.hostObjects) return;
        const result = JSON.parse(await window.chrome.webview.hostObjects.backend.ResetAllOc());
        showModeStatus(result.success ? `↩ ${result.message}` : `⚠️ ${result.message}`);
        loadOcInfo(); // refresh sliders
    });

    // ══════ OC SCANNER LOGIC ══════
    let wasScanning = false;

    async function pollOcScanner() {
        if (!window.chrome?.webview?.hostObjects) return;
        try {
            const raw = await window.chrome.webview.hostObjects.backend.GetOcScanStatus();
            const status = JSON.parse(raw);
            
            if (status.message) {
                document.getElementById('ocScanStatusMsg').textContent = status.message;
            }

            if (status.isScanning) {
                document.getElementById('startOcScanBtn').style.display = 'none';
                document.getElementById('cancelOcScanBtn').style.display = 'block';
                document.getElementById('ocScanProgressContainer').style.display = 'block';
                document.getElementById('ocScanProgressBar').style.width = status.progress + '%';
                
                // disable sliders
                document.getElementById('ocCoreSlider').disabled = true;
                document.getElementById('ocMemSlider').disabled = true;
                document.getElementById('applyCoreBtn').disabled = true;
                document.getElementById('applyMemBtn').disabled = true;
                document.getElementById('resetOcBtn').disabled = true;
            } else {
                document.getElementById('startOcScanBtn').style.display = 'block';
                document.getElementById('cancelOcScanBtn').style.display = 'none';
                if (status.progress === 0 && !status.message) {
                     document.getElementById('ocScanProgressContainer').style.display = 'none';
                } else if (status.progress === 100) {
                     document.getElementById('ocScanProgressBar').style.width = '100%';
                     document.getElementById('ocScanProgressBar').parentNode.classList.replace('orange', 'green');
                }
                
                // re-enable sliders (except when unsupported)
                if (ocInfo && ocInfo.supported) {
                    document.getElementById('ocCoreSlider').disabled = false;
                    document.getElementById('ocMemSlider').disabled = false;
                    document.getElementById('applyCoreBtn').disabled = false;
                    document.getElementById('applyMemBtn').disabled = false;
                    document.getElementById('resetOcBtn').disabled = false;
                }

                // If we just finished a scan, refresh the settings
                if (wasScanning && status.progress === 100) {
                    loadOcInfo();
                }
            }
            wasScanning = status.isScanning;
        } catch(e) {}
    }
    
    setInterval(pollOcScanner, 1000);

    document.getElementById('startOcScanBtn').addEventListener('click', () => {
        if (!window.chrome?.webview?.hostObjects) return;
        window.chrome.webview.hostObjects.backend.StartOcScan();
        document.getElementById('ocScanProgressContainer').style.display = 'block';
        document.getElementById('ocScanProgressBar').style.width = '0%';
        document.getElementById('ocScanProgressBar').parentNode.classList.replace('green', 'orange');
    });

    document.getElementById('cancelOcScanBtn').addEventListener('click', () => {
        if (!window.chrome?.webview?.hostObjects) return;
        window.chrome.webview.hostObjects.backend.CancelOcScan();
    });

    // ══════ STRESS TEST LOGIC (GPU / CPU / COMBINED) ══════
    let currentStressMode = 'combined'; // 'gpu', 'cpu', 'combined'
    
    function getGradeColor(grade) {
        if (!grade) return '#888';
        if (grade.startsWith('A')) return '#44ff88';
        if (grade.startsWith('B')) return '#88ccff';
        if (grade === 'C') return '#ffcc44';
        if (grade === 'D') return '#ff8844';
        return '#ff4444';
    }

    function setTelemetryText(id, value, suffix) {
        const el = document.getElementById(id);
        if (el) el.textContent = (value !== null && value !== undefined) ? (typeof value === 'number' ? value.toFixed(1) : value) + (suffix || '') : '--';
    }

    function updateGpuTelemetry(status) {
        setTelemetryText('telGpuMaxTemp', status.maxGpuTemp, 'C');
        setTelemetryText('telGpuAvgTemp', status.avgGpuTemp, 'C');
        setTelemetryText('telGpuMinTemp', status.minGpuTemp, 'C');
        setTelemetryText('telGpuMaxClock', status.maxGpuClock, ' MHz');
        setTelemetryText('telGpuAvgClock', status.avgGpuClock, ' MHz');
        setTelemetryText('telGpuMaxPower', status.maxGpuPower, 'W');
        setTelemetryText('telGpuAvgPower', status.avgGpuPower, 'W');
        setTelemetryText('telGpuMaxFan', status.maxGpuFanRpm, ' RPM');
        setTelemetryText('telGpuMaxVram', status.maxVramMb, ' MB');
        setTelemetryText('telGpuThrottles', status.throttleEvents || status.gpuThrottleEvents, '');
    }

    function updateCpuTelemetry(status) {
        setTelemetryText('telCpuMaxTemp', status.maxCpuTemp, 'C');
        setTelemetryText('telCpuAvgTemp', status.avgCpuTemp, 'C');
        setTelemetryText('telCpuMinTemp', status.minCpuTemp, 'C');
        setTelemetryText('telCpuMaxClock', status.maxCpuClock, ' MHz');
        setTelemetryText('telCpuAvgClock', status.avgCpuClock, ' MHz');
        setTelemetryText('telCpuMaxPower', status.maxCpuPower, 'W');
        setTelemetryText('telCpuAvgPower', status.avgCpuPower, 'W');
        setTelemetryText('telCpuThrottles', status.throttleEvents || status.cpuThrottleEvents, '');
    }

    function showGradeBadge(grade, score, label) {
        const badge = document.getElementById('stressGradeBadge');
        if (!badge || !grade) return;
        badge.style.display = 'inline-block';
        badge.style.background = getGradeColor(grade);
        badge.style.color = (grade.startsWith('A') || grade.startsWith('B')) ? '#111' : '#fff';
        badge.textContent = `${label || 'GRADE'}: ${grade}` + (score !== null && score !== undefined ? ` | SCORE: ${score}` : '');
    }

    function buildScoreHtml(status) {
        let html = '';
        if (currentStressMode === 'combined') {
            const msg = (status.gpuMessage || '') + ' / ' + (status.cpuMessage || '');
            html = msg;
            if (status.combinedScore !== null && status.combinedScore !== undefined) {
                html += `<br><span style="display:inline-block; margin-top:6px; background:linear-gradient(90deg, #ff8800, #ff4444); color:white; padding:4px 10px; border-radius:4px; font-weight:800; letter-spacing:1px; box-shadow: 0 4px 12px rgba(255,100,0,0.3);">COMBINED SCORE: ${status.combinedScore}</span>`;
            }
        } else if (currentStressMode === 'gpu' && status.rigScore !== null && status.rigScore !== undefined) {
            html = (status.message || '').split('RIG Score')[0];
            html += `<br><span style="display:inline-block; margin-top:6px; background:linear-gradient(90deg, #ff8800, #ff4444); color:white; padding:4px 10px; border-radius:4px; font-weight:800; letter-spacing:1px; box-shadow: 0 4px 12px rgba(255,100,0,0.3);">RIG SCORE: ${status.rigScore}</span>`;
        } else if (currentStressMode === 'cpu' && status.cpuScore !== null && status.cpuScore !== undefined) {
            html = (status.message || '').split('Score')[0];
            html += `<br><span style="display:inline-block; margin-top:6px; background:linear-gradient(90deg, #4488ff, #44ccff); color:white; padding:4px 10px; border-radius:4px; font-weight:800; letter-spacing:1px; box-shadow: 0 4px 12px rgba(68,136,255,0.3);">CPU SCORE: ${status.cpuScore}</span>`;
        }
        return html;
    }

    async function pollStressTest() {
        if (!window.chrome?.webview?.hostObjects) return;
        try {
            let raw, status;
            const mode = currentStressMode;

            if (mode === 'combined') {
                raw = await window.chrome.webview.hostObjects.backend.GetCombinedStressTestStatus();
                status = JSON.parse(raw);
            } else if (mode === 'cpu') {
                raw = await window.chrome.webview.hostObjects.backend.GetCpuStressTestStatus();
                status = JSON.parse(raw);
            } else {
                raw = await window.chrome.webview.hostObjects.backend.GetStressTestStatus();
                status = JSON.parse(raw);
            }
            
            const msgEl = document.getElementById('stressStatusMsg');
            const telPanel = document.getElementById('stressTelemetryPanel');
            const gpuSection = document.getElementById('telemetryGpuSection');
            const cpuSection = document.getElementById('telemetryCpuSection');

            // Show/hide telemetry sections based on mode
            if (gpuSection) gpuSection.style.display = (mode === 'cpu') ? 'none' : 'block';
            if (cpuSection) cpuSection.style.display = (mode === 'gpu') ? 'none' : 'block';

            if (status.isStressing) {
                document.getElementById('startStressBtn').closest('.stat-card').classList.add('stress-active');
                document.getElementById('startStressBtn').style.display = 'none';
                document.getElementById('cancelStressBtn').style.display = 'block';
                document.getElementById('stressProgressContainer').style.display = 'block';
                document.getElementById('stressProgressBar').style.width = status.progress + '%';
                document.getElementById('stressDuration').disabled = true;
                document.getElementById('stressMode').disabled = true;
                const maxTempEl = document.getElementById('stressMaxTemp');
                if (maxTempEl) maxTempEl.disabled = true;
                msgEl.style.color = 'var(--text-main)';

                // Show live telemetry during test
                if (telPanel) telPanel.style.display = 'block';
                
                if (mode === 'combined') {
                    msgEl.textContent = (status.gpuMessage || 'GPU...') + ' | ' + (status.cpuMessage || 'CPU...');
                    updateGpuTelemetry(status);
                    updateCpuTelemetry(status);
                } else if (mode === 'cpu') {
                    if (status.message) msgEl.textContent = status.message;
                    updateCpuTelemetry(status);
                } else {
                    if (status.message) msgEl.textContent = status.message;
                    updateGpuTelemetry(status);
                }
            } else {
                document.getElementById('startStressBtn').closest('.stat-card').classList.remove('stress-active');
                document.getElementById('startStressBtn').style.display = 'block';
                document.getElementById('cancelStressBtn').style.display = 'none';
                document.getElementById('stressDuration').disabled = false;
                document.getElementById('stressMode').disabled = false;
                const maxTempEl = document.getElementById('stressMaxTemp');
                if (maxTempEl) maxTempEl.disabled = false;
                
                if (status.progress === 0 && !status.message && !status.gpuMessage && !status.cpuMessage) {
                     document.getElementById('stressProgressContainer').style.display = 'none';
                     if (telPanel) telPanel.style.display = 'none';
                } else if (status.progress === 100) {
                     document.getElementById('stressProgressBar').style.width = '100%';
                     if (telPanel) telPanel.style.display = 'block';

                     // Update final telemetry
                     if (mode === 'combined' || mode === 'gpu') updateGpuTelemetry(status);
                     if (mode === 'combined' || mode === 'cpu') updateCpuTelemetry(status);

                     // Determine color from grade/result
                     const grade = status.stabilityGrade || status.combinedGrade || '';
                     if (grade.startsWith('A') || grade.startsWith('B')) {
                         msgEl.style.color = '#44ff88';
                     } else if (grade === 'C') {
                         msgEl.style.color = '#ffcc44';
                     } else {
                         msgEl.style.color = '#ff6677';
                     }

                     // Show score badges
                     const scoreHtml = buildScoreHtml(status);
                     if (scoreHtml) msgEl.innerHTML = scoreHtml;

                     // Show grade badge
                     if (mode === 'combined') {
                         showGradeBadge(status.combinedGrade, status.combinedScore, 'COMBINED');
                     } else if (mode === 'gpu') {
                         showGradeBadge(status.stabilityGrade, status.rigScore, 'GPU');
                     } else {
                         showGradeBadge(status.stabilityGrade, status.cpuScore, 'CPU');
                     }
                }
            }
        } catch(e) {}
    }

    setInterval(pollStressTest, 1000);

    // Track mode selection
    document.getElementById('stressMode')?.addEventListener('change', (e) => {
        currentStressMode = e.target.value;
        // Hide telemetry panel when switching modes
        const telPanel = document.getElementById('stressTelemetryPanel');
        if (telPanel) telPanel.style.display = 'none';
        const badge = document.getElementById('stressGradeBadge');
        if (badge) badge.style.display = 'none';
        document.getElementById('stressStatusMsg').textContent = 'Ready';
        document.getElementById('stressStatusMsg').style.color = 'var(--accent-main)';
        document.getElementById('stressProgressContainer').style.display = 'none';
    });

    document.getElementById('startStressBtn')?.addEventListener('click', () => {
        if (!window.chrome?.webview?.hostObjects) return;
        const seconds = parseInt(document.getElementById('stressDuration').value) || 60;
        const maxTemp = parseInt(document.getElementById('stressMaxTemp')?.value) || 85;
        const mode = document.getElementById('stressMode')?.value || 'gpu';
        currentStressMode = mode;

        // Reset telemetry display
        const badge = document.getElementById('stressGradeBadge');
        if (badge) badge.style.display = 'none';

        if (mode === 'combined') {
            window.chrome.webview.hostObjects.backend.StartCombinedStressTest(seconds, maxTemp);
        } else if (mode === 'cpu') {
            window.chrome.webview.hostObjects.backend.StartCpuStressTest(seconds, maxTemp);
        } else {
            window.chrome.webview.hostObjects.backend.StartStressTest(seconds, maxTemp);
        }
        document.getElementById('stressProgressContainer').style.display = 'block';
        document.getElementById('stressProgressBar').style.width = '0%';
        document.getElementById('stressStatusMsg').style.color = 'var(--accent-main)';
        document.getElementById('stressStatusMsg').textContent = 'Starting...';
    });

    document.getElementById('cancelStressBtn')?.addEventListener('click', () => {
        if (!window.chrome?.webview?.hostObjects) return;
        const mode = currentStressMode;
        if (mode === 'combined') {
            window.chrome.webview.hostObjects.backend.CancelCombinedStressTest();
        } else if (mode === 'cpu') {
            window.chrome.webview.hostObjects.backend.CancelCpuStressTest();
        } else {
            window.chrome.webview.hostObjects.backend.CancelStressTest();
        }
    });

    // AI Approve OC function (called from AI-generated buttons)
    window.approveOc = async function(type, value) {
        if (!window.chrome?.webview?.hostObjects) {
            showError('Backend offline');
            return;
        }
        let result;
        try {
            if (type === 'core') {
                result = JSON.parse(await window.chrome.webview.hostObjects.backend.ApplyGpuCoreClock(Math.round(value)));
            } else if (type === 'mem') {
                result = JSON.parse(await window.chrome.webview.hostObjects.backend.ApplyGpuMemClock(Math.round(value)));
            } else if (type === 'power') {
                if (!ocInfo) return;
                const watts = ocInfo.defaultPowerW * (value / 100);
                result = JSON.parse(await window.chrome.webview.hostObjects.backend.ApplyGpuPowerLimit(watts));
            }
            if (result && result.success) {
                appendMessage('assistant', `✅ ${result.message}`, true);
                showModeStatus(`⚡ ${result.message}`);
                loadOcInfo(); // refresh OC display
            } else {
                appendMessage('assistant', `⚠️ ${result?.message || 'Failed to apply OC'}`, true);
            }
        } catch(e) {
            showError('OC apply failed: ' + e.message);
        }
    };
    
    // ══════ FAN CURVE EDITOR LOGIC ══════
    const fpCanvas = document.getElementById('fanCurveCanvas');
    if (fpCanvas) {
        const fpCtx = fpCanvas.getContext('2d');
        
        let curvePoints = [
            { x: 30, y: 30 },
            { x: 50, y: 50 },
            { x: 70, y: 75 },
            { x: 85, y: 100 }
        ];
        
        function getX(temp) { return (temp / 100) * fpCanvas.width; }
        function getY(speed) { return fpCanvas.height - ((speed / 100) * fpCanvas.height); }
        function getTemp(x) { return Math.max(20, Math.min(100, (x / fpCanvas.width) * 100)); }
        function getSpeed(y) { return Math.max(20, Math.min(100, 100 - (y / fpCanvas.height) * 100)); }

        let draggingPoint = null;

        function drawCurve() {
            fpCtx.clearRect(0, 0, fpCanvas.width, fpCanvas.height);
            
            // Grid lines
            fpCtx.strokeStyle = 'rgba(255,255,255,0.05)';
            fpCtx.lineWidth = 1;
            for(let i=10; i<=100; i+=10) {
                fpCtx.beginPath(); fpCtx.moveTo(0, getY(i)); fpCtx.lineTo(fpCanvas.width, getY(i)); fpCtx.stroke();
                fpCtx.beginPath(); fpCtx.moveTo(getX(i), 0); fpCtx.lineTo(getX(i), fpCanvas.height); fpCtx.stroke();
            }

            // Labels
            fpCtx.fillStyle = 'rgba(255,255,255,0.3)';
            fpCtx.font = '12px Space Grotesk';
            fpCtx.fillText('100°C', fpCanvas.width - 40, fpCanvas.height - 10);
            fpCtx.fillText('100%', 10, 20);

            // Curve Line
            fpCtx.beginPath();
            curvePoints.sort((a,b) => a.x - b.x);
            fpCtx.moveTo(0, getY(curvePoints[0].y));
            curvePoints.forEach(p => fpCtx.lineTo(getX(p.x), getY(p.y)));
            fpCtx.lineTo(fpCanvas.width, getY(curvePoints[curvePoints.length-1].y));
            
            fpCtx.strokeStyle = '#00d4ff'; // var(--accent-main) fallback
            const compStyles = window.getComputedStyle(document.body);
            fpCtx.strokeStyle = compStyles.getPropertyValue('--accent-main') || '#00d4ff';
            fpCtx.lineWidth = 3;
            fpCtx.stroke();

            // Gradient Fill
            let grad = fpCtx.createLinearGradient(0, 0, 0, fpCanvas.height);
            grad.addColorStop(0, fpCtx.strokeStyle.replace('rgb', 'rgba').replace(')', ', 0.3)'));
            grad.addColorStop(1, 'rgba(0,0,0,0)');
            fpCtx.lineTo(fpCanvas.width, fpCanvas.height);
            fpCtx.lineTo(0, fpCanvas.height);
            fpCtx.fillStyle = grad;
            fpCtx.fill();

            // Points
            curvePoints.forEach((p, idx) => {
                fpCtx.beginPath();
                fpCtx.arc(getX(p.x), getY(p.y), draggingPoint === idx ? 8 : 6, 0, Math.PI * 2);
                fpCtx.fillStyle = draggingPoint === idx ? '#fff' : fpCtx.strokeStyle;
                fpCtx.fill();
                fpCtx.strokeStyle = 'rgba(255,255,255,0.5)';
                fpCtx.lineWidth = 2;
                fpCtx.stroke();
                
                fpCtx.fillStyle = '#fff';
                fpCtx.fillText(`${Math.round(p.x)}°C / ${Math.round(p.y)}%`, getX(p.x) - 15, getY(p.y) - 15);
            });
        }

        fpCanvas.addEventListener('mousedown', (e) => {
            const rect = fpCanvas.getBoundingClientRect();
            const scaleX = fpCanvas.width / rect.width;
            const scaleY = fpCanvas.height / rect.height;
            const x = (e.clientX - rect.left) * scaleX;
            const y = (e.clientY - rect.top) * scaleY;

            draggingPoint = curvePoints.findIndex(p => Math.hypot(getX(p.x) - x, getY(p.y) - y) < 20);
            if (draggingPoint !== -1) drawCurve();
        });

        window.addEventListener('mouseup', () => {
            if (draggingPoint !== null) { draggingPoint = null; drawCurve(); }
        });

        fpCanvas.addEventListener('mousemove', (e) => {
            if (draggingPoint === null) return;
            const rect = fpCanvas.getBoundingClientRect();
            const scaleX = fpCanvas.width / rect.width;
            const scaleY = fpCanvas.height / rect.height;
            const x = (e.clientX - rect.left) * scaleX;
            const y = (e.clientY - rect.top) * scaleY;
            
            curvePoints[draggingPoint].x = getTemp(x);
            curvePoints[draggingPoint].y = getSpeed(y);
            
            if (draggingPoint > 0 && curvePoints[draggingPoint].x <= curvePoints[draggingPoint-1].x) {
                curvePoints[draggingPoint].x = curvePoints[draggingPoint-1].x + 1;
            }
            if (draggingPoint < curvePoints.length - 1 && curvePoints[draggingPoint].x >= curvePoints[draggingPoint+1].x) {
                curvePoints[draggingPoint].x = curvePoints[draggingPoint+1].x - 1;
            }
            
            drawCurve();
        });

        // initial draw delayed to let styles compute
        setTimeout(drawCurve, 100);

        // re-draw when switching modes to update the accent color
        document.querySelectorAll('.mode-btn').forEach(btn => btn.addEventListener('click', () => setTimeout(drawCurve, 50)));

        document.getElementById('applyFanCurveBtn').addEventListener('click', async () => {
            if (!window.chrome?.webview?.hostObjects) return;
            const curveData = curvePoints.map(p => ({ temp: Math.round(p.x), speed: Math.round(p.y) }));
            try {
                const resultJson = await window.chrome.webview.hostObjects.backend.ApplyCustomFanCurve(JSON.stringify(curveData));
                const result = JSON.parse(resultJson);
                showModeStatus(result.success ? `✅ System fan curve applied` : `⚠️ ${result.message}`);
            } catch(e) {
                showError('Failed to send fan curve to background service');
            }
        });
    }

    // ══════ GAME AUTO-SWITCHING ══════
    let autoSwitchEnabled = false;
    const autoSwitchBtn = document.getElementById('autoSwitchBtn');

    async function loadAutoSwitchStatus() {
        if (!window.chrome?.webview?.hostObjects) return;
        try {
            autoSwitchEnabled = await window.chrome.webview.hostObjects.backend.GetAutoSwitching();
            updateAutoSwitchBtn();
        } catch(e) {}
    }

    function updateAutoSwitchBtn() {
        if (!autoSwitchBtn) return;
        if (autoSwitchEnabled) {
            autoSwitchBtn.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" style="margin-right:6px;"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg> Auto-Game: ON`;
            autoSwitchBtn.style.background = 'rgba(0, 212, 255, 0.1)';
            autoSwitchBtn.style.borderColor = 'rgba(0, 212, 255, 0.3)';
            autoSwitchBtn.style.color = 'var(--accent-main)';
        } else {
            autoSwitchBtn.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" style="margin-right:6px;"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg> Auto-Game: OFF`;
            autoSwitchBtn.style.background = 'rgba(255,255,255,0.05)';
            autoSwitchBtn.style.borderColor = 'rgba(255,255,255,0.1)';
            autoSwitchBtn.style.color = 'var(--text-muted)';
        }
    }

    autoSwitchBtn?.addEventListener('click', async () => {
        if (!window.chrome?.webview?.hostObjects) return;
        try {
            const newState = !autoSwitchEnabled;
            autoSwitchEnabled = await window.chrome.webview.hostObjects.backend.SetAutoSwitching(newState);
            updateAutoSwitchBtn();
            showModeStatus(autoSwitchEnabled ? '🎮 Auto-Game Switching Enabled' : '⏸️ Auto-Game Switching Disabled');
        } catch(e) {
            showError('Auto-switch error: ' + e.message);
        }
    });

    setTimeout(loadAutoSwitchStatus, 1500);

});
