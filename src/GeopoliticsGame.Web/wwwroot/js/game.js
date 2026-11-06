
(function () {
    'use strict';

    const app = document.getElementById('game-app');
    if (!app || !window.geo) return;

    const { api, escapeHtml: esc, toast } = window.geo;
    const API = app.dataset.api;
    const CAN_PLAY = app.dataset.canPlay === 'true';
    const HUMAN = 'human';
    const POLL_MS = 3000;
    const $ = id => document.getElementById(id);

    const PROFILE = { aggressive: '🔥 Aggressive', defensive: '🛡️ Defensive', balanced: '⚖️ Balanced' };
    const BATTLE_TYPES = new Set(['attack', 'round', 'conquer', 'repelled', 'retreat', 'eliminate', 'win']);

    const ui = {
        game: null,         
        view: null,          
        busy: false,
        replaying: false,
        mode: null,          
        from: null,
        to: null,
        amount: 1,
        deploy: {},          
        afterConquest: null, 
        stage: null,         
        feedFilter: 'all',
        boardFilter: 'all',
        boardSort: 'soldiers',
        fastForward: localStorage.getItem('geo.fastForward') === '1'
    };

    // ───────────────────────────── helpers ─────────────────────────────

    const clone = o => JSON.parse(JSON.stringify(o));
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    const plural = (n, w, pl) => `${n} ${n === 1 ? w : (pl || w + 's')}`;
    const player = (id, g = ui.view) => g.players.find(p => p.id === id);
    const territory = (code, g = ui.view) => g.board.find(t => t.countryCode === code);
    const currentId = (g = ui.view) => g.turnOrder[g.currentTurnIndex];
    const mine = (g = ui.view) => g.board.filter(t => t.ownerId === HUMAN);
    const me = () => player(HUMAN, ui.game);
    const isMyTurn = () => CAN_PLAY && !ui.replaying && ui.game?.status === 'active' && currentId(ui.game) === HUMAN;
    const draftTotal = () => Object.values(ui.deploy).reduce((a, b) => a + b, 0);
    const reward = t => Math.max(2, Math.floor(t.soldierValue / 2));
    const popLabel = p => p >= 1e6 ? `${(p / 1e6).toFixed(p >= 1e8 ? 0 : 1)}M` : p >= 1e3 ? `${Math.round(p / 1e3)}K` : `${p}`;
    const diceFor = (a, d) => Math.max(0, Math.min(a - 1, d));

    function resetSelection() {
        ui.mode = null;
        ui.from = null;
        ui.to = null;
        ui.amount = 1;
    }

    function setGame(game) {
        ui.game = game;
        ui.view = clone(game);
        if (!isMyTurn()) {
            resetSelection();
            ui.afterConquest = null;
            ui.deploy = {};
        }
      
        for (const code of Object.keys(ui.deploy)) {
            if (territory(code, game)?.ownerId !== HUMAN) delete ui.deploy[code];
        }
       
        const pa = game.pendingAttack;
        if (pa && pa.attackerId === HUMAN && (!ui.stage || ui.stage.toCode !== pa.toCode)) {
            ui.stage = { fromCode: pa.fromCode, toCode: pa.toCode, defenderId: territory(pa.toCode, game)?.ownerId, round: null, commentary: 'The armies face each other. Roll when ready.' };
        }
    }

    // ───────────────────────────── rendering ───────────────────────────

    function renderAll() {
        if (!ui.view) return;
        renderHeader();
        renderStage();
        renderBoard();
        renderPlayers();
        renderFeed();
        renderCommand();
    }

    function renderHeader() {
        const v = ui.view;
        const cur = player(currentId(v));
        $('round-badge').textContent = `Round ${v.round}`;

        const badge = $('turn-badge');
        let text;
        if (ui.game.status !== 'active') text = 'Game over';
        else if (ui.replaying) text = `▶ ${cur.name} is playing…`;
        else if (cur.id === HUMAN) text = CAN_PLAY ? '⭐ Your turn' : `${cur.name}'s turn`;
        else text = `${cur.name}'s turn`;
        badge.textContent = text;
        badge.className = `badge turn-badge ${cur.id === HUMAN && CAN_PLAY && ui.game.status === 'active' ? 'yours' : ''}`;

        const ff = $('ff-btn');
        ff.textContent = `⏩ Fast-forward: ${ui.fastForward ? 'on' : 'off'}`;
        ff.classList.toggle('active', ui.fastForward);
        ff.setAttribute('aria-pressed', String(ui.fastForward));

        const over = $('game-over');
        if (ui.game.status === 'active' || ui.replaying) {
            over.classList.add('d-none');
        } else {
            const won = ui.game.status === 'won';
            const winner = ui.game.winnerId ? player(ui.game.winnerId, ui.game) : null;
            over.className = `alert ${won ? 'alert-success' : 'alert-danger'}`;
            over.innerHTML = won
                ? `🏆 <strong>Victory!</strong> ${esc(ui.game.ownerUsername)} conquered all 10 nations in ${ui.game.round} rounds.`
                : `☠️ <strong>Defeat.</strong> ${winner ? esc(winner.name) + ' conquered the world.' : esc(ui.game.ownerUsername) + ' lost their last country.'}`;
        }
    }

    function selectable(t) {
        if (!isMyTurn() || ui.busy) return false;
        const own = t.ownerId === HUMAN;
        if (me().reinforcementsPending > 0) return own;
        if (ui.mode === 'attack') return ui.from ? !own : own && t.soldiers >= 2;
        if (ui.mode === 'transfer') return ui.from ? own && t.countryCode !== ui.from : own && t.soldiers >= 2;
        return true;
    }

    function renderBoard() {
        const v = ui.view;
        let list = [...v.board];
        if (ui.boardFilter === 'mine') list = list.filter(t => t.ownerId === HUMAN);
        if (ui.boardFilter === 'enemy') list = list.filter(t => t.ownerId !== HUMAN);

        const ownerName = t => player(t.ownerId)?.name ?? '';
        list.sort({
            name: (a, b) => a.name.localeCompare(b.name),
            owner: (a, b) => ownerName(a).localeCompare(ownerName(b)) || b.soldiers - a.soldiers,
            soldiers: (a, b) => b.soldiers - a.soldiers || a.name.localeCompare(b.name)
        }[ui.boardSort]);

        const stage = ui.stage;
        const choosing = isMyTurn() && (ui.mode || me().reinforcementsPending > 0);

        $('board').innerHTML = list.map(t => {
            const owner = player(t.ownerId);
            const classes = ['territory'];
            if (t.ownerId === HUMAN) classes.push('mine');
            if (stage?.fromCode === t.countryCode) classes.push('attacking');
            if (stage?.toCode === t.countryCode) classes.push('defending');
            if (ui.from === t.countryCode) classes.push('selected');
            if (ui.to === t.countryCode) classes.push('targeted');
            if (choosing && !selectable(t)) classes.push('dim');
            const draft = ui.deploy[t.countryCode];
            return `
                <button type="button" class="${classes.join(' ')}" data-code="${esc(t.countryCode)}"
                        style="--owner:${esc(owner?.color ?? '#888')}"
                        aria-label="${esc(t.name)}, ${t.soldiers} soldiers, held by ${esc(owner?.name ?? '')}">
                    <span class="t-top">
                        <span class="t-flag" aria-hidden="true">${esc(t.flag)}</span>
                        <span class="t-soldiers" title="Soldiers">${t.soldiers}${draft ? `<small class="t-draft">+${draft}</small>` : ''}</span>
                    </span>
                    <span class="t-name">${esc(t.name)}</span>
                    <span class="t-owner"><span class="chip" style="background:${esc(owner?.color ?? '#888')}"></span>${esc(owner?.name ?? '')}${t.ownerId === HUMAN ? ' (you)' : ''}</span>
                    <span class="t-meta">${popLabel(t.population)} people · worth ${reward(t)} on capture</span>
                </button>`;
        }).join('') || '<p class="text-secondary small mb-0">No countries match this filter.</p>';
    }

    function renderStage() {
        const el = $('battle-stage');
        const s = ui.stage;
        if (!s) { el.classList.add('d-none'); return; }
        el.classList.remove('d-none');

        const side = (code, role) => {
            const t = territory(code);
            if (!t) return '';
       
            const owner = player(role === 'Defender' && s.defenderId ? s.defenderId : t.ownerId);
            return `
                <div class="side-role">${role}</div>
                <div class="side-flag">${esc(t.flag)}</div>
                <div class="side-name">${esc(t.name)}</div>
                <div class="side-owner"><span class="chip" style="background:${esc(owner?.color ?? '#888')}"></span>${esc(owner?.name ?? '')}</div>
                <div class="side-soldiers">${t.soldiers}</div>`;
        };
        $('stage-attacker').innerHTML = side(s.fromCode, 'Attacker');
        $('stage-defender').innerHTML = side(s.toCode, 'Defender');

        const r = s.round;
        if (r) {
            $('stage-dice').innerHTML = r.attackerDice.map((a, i) => {
                const d = r.defenderDice[i];
                const res = a > d ? ['win', 'lose'] : d > a ? ['lose', 'win'] : ['tie', 'tie'];
                return `<div class="dice-pair">
                    <span class="die die-attacker ${res[0]}">${a}</span>
                    <span class="vs">vs</span>
                    <span class="die die-defender ${res[1]}">${d}</span>
                </div>`;
            }).join('') + `<div class="round-losses">Attacker −${r.attackerLosses} · Defender −${r.defenderLosses}${r.ties ? ` · ${plural(r.ties, 'tie')}` : ''}</div>`;
        } else {
            const a = territory(s.fromCode), d = territory(s.toCode);
            $('stage-dice').innerHTML = a && d
                ? `<div class="text-secondary small">Next roll: ${plural(diceFor(a.soldiers, d.soldiers), 'die', 'dice')} each</div>`
                : '';
        }

        const outcome = {
            conquered: '<span class="badge bg-success">Conquered!</span>', conquer: '<span class="badge bg-success">Conquered!</span>',
            repelled: '<span class="badge bg-danger">Repelled</span>', retreat: '<span class="badge bg-secondary">Retreat</span>'
        }[s.outcome] ?? '';
        $('stage-commentary').innerHTML = `${outcome} ${s.commentary ? `<span>🎙️ ${esc(s.commentary)}</span>` : ''}`;
    }

    function renderPlayers() {
        const v = ui.view;
        const cur = currentId(v);
        const rows = v.turnOrder.map(id => {
            const p = player(id);
            const held = v.board.filter(t => t.ownerId === id);
            return { p, countries: held.length, soldiers: held.reduce((a, t) => a + t.soldiers, 0) };
        });

        $('players').innerHTML = `<ol class="standings list-unstyled mb-0">${rows.map(({ p, countries, soldiers }) => `
            <li class="${p.eliminated ? 'out' : ''} ${p.id === cur && ui.game.status === 'active' ? 'current' : ''}">
                <span class="chip" style="background:${esc(p.color)}"></span>
                <span class="name">${esc(p.name)}${p.id === HUMAN ? ' <span class="badge bg-primary">You</span>' : ''}</span>
                ${p.type === 'npc' ? `<span class="profile">${PROFILE[p.profile] ?? ''}</span>` : ''}
                <span class="nums">${p.eliminated ? 'eliminated' : `${plural(countries, 'country', 'countries')} · ${soldiers}🪖`}</span>
            </li>`).join('')}</ol>
            <p class="small text-secondary mt-2 mb-0">Listed in turn order (decided at random).</p>`;
    }

    function feedCategory(type) {
        if (type === 'decision') return 'decision';
        if (type === 'commentary') return 'commentary';
        if (BATTLE_TYPES.has(type)) return 'battle';
        return 'other';
    }

    function renderFeed() {
        const feed = $('feed');
        const nearBottom = feed.scrollHeight - feed.scrollTop - feed.clientHeight < 60;
        const entries = ui.view.log
            .filter(e => ui.feedFilter === 'all' || feedCategory(e.type) === ui.feedFilter)
            .slice(-200);

        feed.innerHTML = entries.map(e => {
            const dice = e.round
                ? `<span class="mini-dice">${e.round.attackerDice.map((a, i) => `<b>${a}</b>:<b>${e.round.defenderDice[i]}</b>`).join(' ')}</span>`
                : '';
            return `<li class="feed-item feed-${esc(e.type)}">${esc(e.message)}${dice}</li>`;
        }).join('') || '<li class="text-secondary small">Nothing here yet.</li>';

        if (nearBottom || ui.replaying) feed.scrollTop = feed.scrollHeight;
    }

    // ───────────────────────────── command panel ───────────────────────

    function renderCommand() {
        const body = $('command-body');
        if (!body || !ui.game) return;

        const g = ui.game;
        const busy = ui.busy ? 'disabled' : '';
        const btn = (action, label, cls = 'btn-outline-light', extra = '') =>
            `<button type="button" class="btn btn-sm ${cls}" data-action="${action}" ${busy} ${extra}>${label}</button>`;

        if (g.status !== 'active') {
            body.innerHTML = `<p class="mb-2">This campaign is over.</p><a class="btn btn-sm btn-primary" href="/Games">Start another</a>`;
            return;
        }
        if (ui.replaying) {
            body.innerHTML = `<p class="mb-1">🤖 The NPCs are taking their turns. Every move is shown on the board and in the feed.</p>
                <p class="mb-0">Turn on <strong>⏩ Fast-forward</strong> to speed them up.</p>`;
            return;
        }
        if (currentId(g) !== HUMAN) {
            body.innerHTML = `<p class="mb-2">Waiting for ${esc(player(currentId(g), g).name)}…</p>${btn('npc-turns', '▶ Play NPC turns', 'btn-primary')}`;
            return;
        }

        const my = me();

      
        if (my.reinforcementsPending > 0) {
            const remaining = my.reinforcementsPending - draftTotal();
            body.innerHTML = `
                <p class="mb-2 text-light">🪖 You have <strong>${plural(my.reinforcementsPending, 'reinforcement')}</strong> to deploy
                   (<strong class="${remaining ? 'text-warning' : 'text-success'}">${remaining} left</strong>).</p>
                <div class="deploy-rows mb-2">${mine(g).map(t => `
                    <div class="deploy-row">
                        <span>${esc(t.flag)} ${esc(t.name)} <span class="text-secondary">(${t.soldiers})</span></span>
                        <span class="btn-group btn-group-sm">
                            <button type="button" class="btn btn-outline-secondary" data-action="deploy-minus" data-code="${esc(t.countryCode)}" ${busy} aria-label="Remove one from ${esc(t.name)}">−</button>
                            <span class="btn btn-outline-secondary disabled deploy-count">${ui.deploy[t.countryCode] || 0}</span>
                            <button type="button" class="btn btn-outline-secondary" data-action="deploy-plus" data-code="${esc(t.countryCode)}" ${busy || (remaining <= 0 ? 'disabled' : '')} aria-label="Add one to ${esc(t.name)}">+</button>
                        </span>
                    </div>`).join('')}
                </div>
                <div class="d-flex flex-wrap gap-2">
                    ${btn('deploy-strongest', 'All on strongest')}
                    ${btn('deploy-spread', 'Spread evenly')}
                    ${btn('deploy-clear', 'Clear', 'btn-outline-secondary')}
                    ${btn('deploy', `Deploy ${draftTotal() || ''}`.trim(), 'btn-primary', draftTotal() ? '' : 'disabled')}
                </div>
                <p class="small text-secondary mt-2 mb-0">Tip: clicking one of your countries on the map adds a soldier to it.</p>`;
            return;
        }

    
        const pa = g.pendingAttack;
        if (pa) {
            const from = territory(pa.fromCode, g), to = territory(pa.toCode, g);
            const dice = diceFor(from.soldiers, to.soldiers);
            body.innerHTML = `
                <p class="mb-1 text-light">⚔️ ${esc(from.name)} <strong>${from.soldiers}</strong> vs ${esc(to.name)} <strong>${to.soldiers}</strong></p>
                <p class="mb-2">Each side rolls ${plural(dice, 'die', 'dice')}. Keep rolling until ${esc(to.name)} falls or you're down to 1 soldier.</p>
                <div class="d-flex gap-2">
                    ${btn('roll', '🎲 Roll', 'btn-danger btn-lg flex-grow-1')}
                    ${btn('retreat', 'Retreat (ends turn)', 'btn-outline-light')}
                </div>`;
            return;
        }

  
        if (ui.afterConquest) {
            const from = territory(ui.afterConquest.fromCode, g), to = territory(ui.afterConquest.toCode, g);
            const max = from && from.ownerId === HUMAN ? from.soldiers - 1 : 0;
            if (to && to.ownerId === HUMAN && max >= 1) {
                ui.amount = Math.min(Math.max(1, ui.amount), max);
                body.innerHTML = `
                    <p class="mb-2 text-light">🏴 ${esc(to.name)} is yours! It holds ${plural(to.soldiers, 'soldier')}.
                       Move more in from ${esc(from.name)} (${from.soldiers})?</p>
                    <div class="d-flex align-items-center gap-2 mb-2">
                        <input type="range" class="form-range" min="1" max="${max}" value="${ui.amount}" data-input="amount" aria-label="Soldiers to move">
                        <strong class="amount-label">${ui.amount}</strong>
                    </div>
                    <div class="d-flex gap-2">
                        ${btn('conquest-move', `Move ${ui.amount}`, 'btn-primary')}
                        ${btn('conquest-skip', 'Not now', 'btn-outline-secondary')}
                    </div>`;
                return;
            }
            ui.afterConquest = null;
        }

    
        if (ui.mode === 'attack') {
            const from = ui.from && territory(ui.from, g), to = ui.to && territory(ui.to, g);
            const ready = from && to;
            body.innerHTML = `
                <p class="mb-1 text-light">⚔️ Plan your attack</p>
                <ol class="steps mb-2">
                    <li class="${from ? 'done' : 'now'}">Attack from: <strong>${from ? `${esc(from.flag)} ${esc(from.name)} (${from.soldiers})` : 'click one of your countries with 2+ soldiers'}</strong></li>
                    <li class="${to ? 'done' : from ? 'now' : ''}">Target: <strong>${to ? `${esc(to.flag)} ${esc(to.name)} (${to.soldiers})` : 'click an enemy country'}</strong></li>
                </ol>
                ${ready ? `<p class="mb-2">${from.soldiers - 1} can fight; first roll is ${plural(diceFor(from.soldiers, to.soldiers), 'die', 'dice')} each. Capture is worth ${reward(to)} soldiers.</p>` : ''}
                <div class="d-flex gap-2">
                    ${btn('launch', '⚔️ Launch attack', 'btn-danger', ready ? '' : 'disabled')}
                    ${btn('cancel', 'Cancel', 'btn-outline-secondary')}
                </div>`;
            return;
        }

        if (ui.mode === 'transfer') {
            const from = ui.from && territory(ui.from, g), to = ui.to && territory(ui.to, g);
            const max = from ? from.soldiers - 1 : 0;
            ui.amount = Math.min(Math.max(1, ui.amount), Math.max(1, max));
            body.innerHTML = `
                <p class="mb-1 text-light">🔁 Move soldiers between your countries</p>
                <ol class="steps mb-2">
                    <li class="${from ? 'done' : 'now'}">From: <strong>${from ? `${esc(from.name)} (${from.soldiers})` : 'click one of your countries with 2+ soldiers'}</strong></li>
                    <li class="${to ? 'done' : from ? 'now' : ''}">To: <strong>${to ? `${esc(to.name)} (${to.soldiers})` : 'click another of your countries'}</strong></li>
                </ol>
                ${from && to ? `
                <div class="d-flex align-items-center gap-2 mb-2">
                    <input type="range" class="form-range" min="1" max="${max}" value="${ui.amount}" data-input="amount" aria-label="Soldiers to move">
                    <strong class="amount-label">${ui.amount}</strong>
                </div>` : ''}
                <div class="d-flex gap-2">
                    ${btn('transfer', `Move ${from && to ? ui.amount : ''}`.trim(), 'btn-primary', from && to ? '' : 'disabled')}
                    ${btn('cancel', 'Cancel', 'btn-outline-secondary')}
                </div>
                <p class="small text-secondary mt-2 mb-0">Every country must keep at least 1 soldier.</p>`;
            return;
        }

      
        const attacked = g.hasAttackedThisTurn;
        const canLaunch = mine(g).some(t => t.soldiers >= 2);
        body.innerHTML = `
            <p class="mb-2 text-light">⭐ Your move, commander.</p>
            <div class="d-grid gap-2">
                ${btn('mode-attack', attacked ? '⚔️ Attack used this turn' : '⚔️ Attack', 'btn-danger', attacked || !canLaunch ? 'disabled' : '')}
                ${btn('mode-transfer', '🔁 Transfer soldiers', 'btn-outline-light', mine(g).length < 2 ? 'disabled' : '')}
                ${btn('end-turn', attacked ? '⏭️ End turn' : '⏭️ Skip turn', 'btn-outline-warning')}
            </div>
            <p class="small text-secondary mt-2 mb-0">${!canLaunch && !attacked
                ? 'None of your countries has the 2 soldiers needed to attack.'
                : 'One attack per turn. You can transfer soldiers before or after it. Tip: click an enemy country to target it.'}</p>`;
    }

    // ───────────────────────────── actions ─────────────────────────────

    async function post(path, body, { replay = false } = {}) {
        if (ui.busy) return null;
        ui.busy = true;
        renderCommand();
        try {
            const res = await api(`${API}/${path}`, { method: 'POST', body });
            if (replay && res.frames?.length) {
                const finalLog = res.game.log;
                ui.game = res.game;
                ui.view.log = finalLog.slice(0, Math.max(0, finalLog.length - res.frames.length));
                await playFrames(res.frames);
            }
            setGame(res.game);
            return res;
        } catch (err) {
            toast(err.message, 'error');
            if (err.status === 400 || err.status === 409) await refresh();
            return null;
        } finally {
            ui.busy = false;
            renderAll();
        }
    }

    function delayFor(type) {
        if (ui.fastForward) return type === 'decision' || type === 'commentary' ? 260 : 130;
        return {
            round: 1000, attack: 950, decision: 1500, commentary: 1600, conquer: 1300, repelled: 1200,
            retreat: 900, eliminate: 1600, win: 1800, deploy: 800, transfer: 850, reinforce: 650, endTurn: 450, system: 700
        }[type] ?? 700;
    }


    async function playFrames(frames) {
        ui.replaying = true;
        resetSelection();
        ui.afterConquest = null;
        ui.stage = null;
        renderAll();

        let battle = null;
        for (const frame of frames) {
            const e = frame.entry;
            for (const s of frame.board) {
                const t = territory(s.code);
                if (t) { t.ownerId = s.ownerId; t.soldiers = s.soldiers; }
            }
            ui.view.currentTurnIndex = Math.max(0, ui.view.turnOrder.indexOf(frame.currentPlayerId));
            ui.view.round = frame.round;
            for (const p of ui.view.players) p.eliminated = frame.eliminatedPlayerIds.includes(p.id);
            ui.view.log.push(e);

            if (e.type === 'attack') {
                battle = { fromCode: e.fromCode, toCode: e.toCode, defenderId: territory(e.toCode)?.ownerId, round: null, commentary: '' };
                ui.stage = battle;
            } else if (battle && e.type === 'round') {
                battle.round = e.round;
            } else if (battle && (e.type === 'conquer' || e.type === 'repelled' || e.type === 'retreat')) {
                battle.outcome = e.type;
            } else if (battle && e.type === 'commentary') {
                battle.commentary = e.message.replace(/^🎙️\s*/, '');
            }

            renderHeader();
            renderStage();
            renderBoard();
            renderPlayers();
            renderFeed();
            await sleep(delayFor(e.type));

            if (e.type === 'endTurn' || e.type === 'win') { ui.stage = null; battle = null; }
        }

        ui.stage = null;
        ui.replaying = false;
    }

    async function refresh() {
        try {
            const res = await api(API);
            setGame(res.game);
            renderAll();
        } catch (err) {
            toast(err.message, 'error');
        }
    }

    function onTerritoryClick(code) {
        if (!isMyTurn() || ui.busy) return;
        const g = ui.game;
        const t = territory(code, g);
        const own = t.ownerId === HUMAN;

        if (me().reinforcementsPending > 0) {
            if (!own) return toast('Deploy your reinforcements on your own countries first.', 'info');
            if (draftTotal() < me().reinforcementsPending) ui.deploy[code] = (ui.deploy[code] || 0) + 1;
            return renderAll();
        }
        if (g.pendingAttack) return;
        ui.afterConquest = null;

        if (!ui.mode) {
            if (own) {
                if (t.soldiers < 2) return toast(`${t.name} has only 1 soldier: it can't attack or send soldiers.`, 'info');
                ui.mode = g.hasAttackedThisTurn ? 'transfer' : 'attack';
                ui.from = code;
            } else {
                if (g.hasAttackedThisTurn) return toast("You've already attacked this turn.", 'info');
                const best = mine(g).filter(m => m.soldiers >= 2).sort((a, b) => b.soldiers - a.soldiers)[0];
                ui.mode = 'attack';
                ui.from = best?.countryCode ?? null;
                ui.to = code;
            }
            return renderAll();
        }

        if (ui.mode === 'attack') {
            if (own) {
                if (t.soldiers < 2) return toast(`${t.name} needs at least 2 soldiers to attack.`, 'info');
                ui.from = code;
            } else {
                ui.to = code;
            }
        } else if (ui.mode === 'transfer') {
            if (!own) return toast('Transfers only work between your own countries.', 'info');
            if (!ui.from || code === ui.from) {
                if (t.soldiers < 2 && code !== ui.from) return toast(`${t.name} has no soldiers to spare.`, 'info');
                ui.from = code === ui.from ? null : code;
                ui.to = null;
            } else {
                ui.to = code;
            }
        }
        renderAll();
    }

    async function onCommand(action, el) {
        const g = ui.game;
        switch (action) {
            case 'deploy-plus': {
                if (draftTotal() < me().reinforcementsPending) ui.deploy[el.dataset.code] = (ui.deploy[el.dataset.code] || 0) + 1;
                return renderAll();
            }
            case 'deploy-minus': {
                const c = el.dataset.code;
                if (ui.deploy[c]) ui.deploy[c]--;
                if (!ui.deploy[c]) delete ui.deploy[c];
                return renderAll();
            }
            case 'deploy-clear':
                ui.deploy = {};
                return renderAll();
            case 'deploy-strongest': {
                const best = mine(g).sort((a, b) => b.soldiers - a.soldiers)[0];
                ui.deploy = { [best.countryCode]: me().reinforcementsPending };
                return renderAll();
            }
            case 'deploy-spread': {
                const list = mine(g).sort((a, b) => a.soldiers - b.soldiers);
                ui.deploy = {};
                for (let i = 0; i < me().reinforcementsPending; i++) {
                    const c = list[i % list.length].countryCode;
                    ui.deploy[c] = (ui.deploy[c] || 0) + 1;
                }
                return renderAll();
            }
            case 'deploy': {
                const res = await post('deploy', { allocation: ui.deploy });
                if (res) { ui.deploy = {}; toast('Reinforcements deployed.', 'success', 2000); renderAll(); }
                return;
            }
            case 'mode-attack':
                resetSelection(); ui.mode = 'attack'; ui.afterConquest = null;
                return renderAll();
            case 'mode-transfer':
                resetSelection(); ui.mode = 'transfer'; ui.afterConquest = null;
                return renderAll();
            case 'cancel':
                resetSelection();
                return renderAll();
            case 'launch': {
                const fromCode = ui.from, toCode = ui.to;
                const res = await post('attack', { fromCode, toCode });
                if (res) {
                    resetSelection();
                    ui.stage = { fromCode, toCode, defenderId: territory(toCode, res.game)?.ownerId, round: null, commentary: 'The armies face each other. Roll when ready.' };
                    renderAll();
                }
                return;
            }
            case 'roll': {
                const pa = g.pendingAttack;
                if (!pa) return;
                const res = await post('roll');
                if (!res) return;
                ui.stage = { fromCode: pa.fromCode, toCode: pa.toCode, defenderId: ui.stage?.defenderId ?? territory(pa.toCode, g)?.ownerId, round: res.round, commentary: res.commentary, outcome: res.outcome };
                if (res.outcome === 'conquered') {
                    ui.afterConquest = { fromCode: pa.fromCode, toCode: pa.toCode };
                    ui.amount = 1;
                    toast(`Conquered! +${res.reward} soldiers garrison your new country.`, 'success');
                } else if (res.outcome === 'repelled') {
                    toast('Your attack was repelled.', 'error');
                }
                return renderAll();
            }
            case 'retreat':
                ui.stage = null;
                return void await post('retreat', undefined, { replay: true });
            case 'end-turn':
                ui.stage = null;
                return void await post('end-turn', undefined, { replay: true });
            case 'npc-turns':
                return void await post('npc-turns', undefined, { replay: true });
            case 'transfer': {
                const res = await post('transfer', { fromCode: ui.from, toCode: ui.to, amount: ui.amount });
                if (res) { resetSelection(); renderAll(); }
                return;
            }
            case 'conquest-move': {
                const { fromCode, toCode } = ui.afterConquest;
                const res = await post('transfer', { fromCode, toCode, amount: ui.amount });
                if (res) { ui.afterConquest = null; renderAll(); }
                return;
            }
            case 'conquest-skip':
                ui.afterConquest = null;
                return renderAll();
        }
    }

    // ───────────────────────────── wiring ──────────────────────────────

    $('board').addEventListener('click', e => {
        const card = e.target.closest('.territory');
        if (card) onTerritoryClick(card.dataset.code);
    });

    $('command-body')?.addEventListener('click', e => {
        const el = e.target.closest('[data-action]');
        if (el && !el.disabled) onCommand(el.dataset.action, el);
    });

    $('command-body')?.addEventListener('input', e => {
        if (e.target.dataset.input !== 'amount') return;
        ui.amount = Number(e.target.value);
        const label = e.target.parentElement.querySelector('.amount-label');
        if (label) label.textContent = ui.amount;
        const moveBtn = $('command-body').querySelector('[data-action="transfer"], [data-action="conquest-move"]');
        if (moveBtn) moveBtn.textContent = `Move ${ui.amount}`;
    });

    $('board-filter').addEventListener('change', e => { ui.boardFilter = e.target.value; renderBoard(); });
    $('board-sort').addEventListener('change', e => { ui.boardSort = e.target.value; renderBoard(); });

    $('feed-filters').addEventListener('click', e => {
        const b = e.target.closest('[data-filter]');
        if (!b) return;
        ui.feedFilter = b.dataset.filter;
        for (const x of $('feed-filters').children) x.classList.toggle('active', x === b);
        renderFeed();
    });

    $('ff-btn').addEventListener('click', () => {
        ui.fastForward = !ui.fastForward;
        localStorage.setItem('geo.fastForward', ui.fastForward ? '1' : '0');
        renderHeader();
    });

    $('share-btn').addEventListener('click', async () => {
        try {
            await navigator.clipboard.writeText(location.href);
            toast('Link copied. Anyone with it can watch this game live.', 'success');
        } catch {
            window.prompt('Copy this link to share the game:', location.href);
        }
    });

    async function poll() {
        if (!document.hidden && !ui.busy && !ui.replaying && ui.game) {
            try {
                const res = await api(API);
                if (res.game.updatedAt !== ui.game.updatedAt) { setGame(res.game); renderAll(); }
            } catch { /* transient */ }
        }
        setTimeout(poll, POLL_MS);
    }

    (async function boot() {
        await refresh();
        if (!ui.game) return;
        if (CAN_PLAY && ui.game.status === 'active' && currentId(ui.game) !== HUMAN) {
            await post('npc-turns', undefined, { replay: true });
        }
        if (!CAN_PLAY) setTimeout(poll, POLL_MS);
    })();
})();
