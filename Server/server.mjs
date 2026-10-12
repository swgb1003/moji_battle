import http from 'node:http';
import { randomBytes, randomInt } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { checkName, sanitizeSpecial, createClaudeGenerator, createFakeGenerator } from './specials.mjs';

export const VERSION = 'moji-fighter-x-rooms-v3';
const inputKinds = ['special', 'cheer'];
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const fail = (status, message) => { throw Object.assign(new Error(message), { status }); };
export function allowOrigin(setting, origin) {
  const list = String(setting || '*').split(',').map(x => x.trim()).filter(Boolean);
  if (list.includes('*')) return '*';
  const match = list.find(p => p === origin || (p.includes('*') && typeof origin === 'string' &&
    new RegExp('^' + p.split('*').map(x => x.replace(/[.+?^${}()|[\]\\]/g, '\\$&')).join('[a-z0-9-]+') + '$').test(origin)));
  return match ? origin : list[0];
}
export function createRoomServer({ now = Date.now, grace = 20000, maxRooms = 1000,
  generateSpecial = process.env.SPECIAL_FAKE === '1' ? createFakeGenerator() : createClaudeGenerator(),
  specialsPerHour = 15, specialsPerDay = Number(process.env.SPECIAL_DAILY_LIMIT || 500) } = {}) {
  const rooms = new Map(), limits = new Map();
  // AI generation costs money per call: per-client hourly limit, a daily total, and a cache for repeated names.
  const specialLimits = new Map(), specialCache = new Map(), pending = new Map();
  let specialDay = { count: 0, reset: now() + 86400000 };
  const segmenter = new Intl.Segmenter('ja', { granularity: 'grapheme' });
  function build(b) {
    if (!b || typeof b.character !== 'string' || b.character.length > 12 ||
        [...segmenter.segment(b.character.trim())].length !== 1) fail(400, '文字を一つ選んでください。');
    for (const [key, max] of Object.entries({ fontType: 3, weaponSize: 3, gripType: 3, battleStyle: 3, weaponFlip: 3 }))
      if (!Number.isInteger(b[key]) || b[key] < 0 || b[key] > max) fail(400, '装備の設定を確認してください。');
    for (const value of [b.gripPosition, b.gripPoint?.x, b.gripPoint?.y])
      if (!Number.isFinite(value) || value < 0 || value > 1) fail(400, '持ち手の位置を確認してください。');
    return { character: b.character.trim(), fontType: b.fontType, weaponSize: b.weaponSize, gripType: b.gripType,
      battleStyle: b.battleStyle, weaponFlip: b.weaponFlip, gripPosition: b.gripPosition,
      gripPoint: { x: b.gripPoint.x, y: b.gripPoint.y }, customGrip: Boolean(b.customGrip),
      special: sanitizeSpecial(b.special, b.character.trim()) };
  }
  function sweep() {
    for (const [code, r] of rooms) {
      if (now() - r.players[0].seen > grace || now() - r.created > 24 * 3600000) { rooms.delete(code); continue; }
      if (r.players[1] && now() - r.players[1].seen > grace) {
        r.players[1] = null; r.players[0].ready = false; r.stage = 'lobby'; r.frame = null; r.inputs = []; r.revision++;
      }
    }
  }
  function view(r, side, credential = false) {
    return { code: r.code, side, token: credential ? r.players[side].token : '', revision: r.revision,
      stage: r.stage, matchId: r.matchId, seed: r.seed, frame: r.frame, inputs: r.inputs,
      players: r.players.map(p => ({ joined: !!p, ready: !!p?.ready, build: p?.build ?? null })) };
  }
  const server = http.createServer(async (req, res) => {
    // ALLOWED_ORIGIN: comma-separated origins; '*' inside one matches a single DNS label (e.g. Vercel deployment URLs).
    const allowed = allowOrigin(process.env.ALLOWED_ORIGIN, req.headers.origin);
    res.setHeader('Access-Control-Allow-Origin', allowed);
    if (allowed !== '*') res.setHeader('Vary', 'Origin');
    res.setHeader('Access-Control-Allow-Methods', 'GET,POST,OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type,Authorization');
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    try {
      if (req.method === 'OPTIONS') { res.writeHead(204); res.end(); return; }
      // Behind a hosting proxy (Render) every request shares the proxy address; rate-limit by the client address instead.
      const forwarded = process.env.TRUST_PROXY === '1' ? String(req.headers['x-forwarded-for'] || '').split(',')[0].trim() : '';
      const ip = forwarded || req.socket.remoteAddress, t = now();
      const limit = limits.get(ip) ?? { count: 0, reset: t + 10000 };
      if (t > limit.reset) { limit.count = 0; limit.reset = t + 10000; }
      if (++limit.count > 600) fail(429, '少し待ってから、もう一度お試しください。');
      limits.set(ip, limit);
      if (limits.size > 4096) for (const [k, v] of limits) if (t > v.reset) limits.delete(k);
      sweep();
      const path = new URL(req.url, 'http://local').pathname;
      if (path === '/health' && req.method === 'GET') { res.end(JSON.stringify({ ok: true, version: VERSION, specials: Boolean(generateSpecial) })); return; }
      let body = {};
      if (req.method === 'POST') {
        let size = 0, chunks = [];
        for await (const chunk of req) { size += chunk.length; if (size > 131072) fail(413, '送信データが大きすぎます。'); chunks.push(chunk); }
        try { body = JSON.parse(Buffer.concat(chunks).toString() || '{}'); } catch { fail(400, '送信内容を読み取れません。'); }
      }
      const player = b => ({ token: randomBytes(32).toString('hex'), build: build(b), ready: false, seen: now() });
      let result;
      if (path === '/specials' && req.method === 'POST') {
        if (body.version !== VERSION) fail(409, 'ゲームのバージョンをそろえてください。');
        const name = checkName(body.name, body.character), character = body.character.trim();
        if (!generateSpecial) fail(503, 'AI の技づくりは準備中です。');
        const key = `${character}
${name}`;
        let made = specialCache.get(key);
        if (!made) {
          const quota = specialLimits.get(ip) ?? { count: 0, reset: t + 3600000 };
          if (t > quota.reset) { quota.count = 0; quota.reset = t + 3600000; }
          if (t > specialDay.reset) specialDay = { count: 0, reset: t + 86400000 };
          if (quota.count >= specialsPerHour) fail(429, 'AI の技づくりは 1 時間に ' + specialsPerHour + ' 回までです。');
          if (specialDay.count >= specialsPerDay) fail(429, '今日の AI の技づくりは上限に達しました。明日お試しください。');
          quota.count++; specialDay.count++; specialLimits.set(ip, quota);
          if (specialLimits.size > 4096) for (const [k, v] of specialLimits) if (t > v.reset) specialLimits.delete(k);
          // Same name requested twice at once: share one AI call.
          let job = pending.get(key);
          if (!job) { job = generateSpecial({ character, name }); pending.set(key, job); }
          let raw;
          try { raw = await job; } finally { pending.delete(key); }
          if (!raw || raw.ok === false) fail(422, (typeof raw?.reason === 'string' && raw.reason.trim().slice(0, 60)) || 'この技名では作れませんでした。');
          made = sanitizeSpecial({ ...raw, name }, character);
          if (!made) fail(502, 'AI で技を作れませんでした。');
          specialCache.set(key, made);
          if (specialCache.size > 2000) specialCache.delete(specialCache.keys().next().value);
        }
        result = { special: made };
      } else if (path === '/rooms' && req.method === 'POST') {
        if (body.version !== VERSION) fail(409, 'ゲームのバージョンをそろえてください。');
        if (rooms.size >= maxRooms) fail(503, '現在、部屋がいっぱいです。');
        let code;
        do { code = Array.from({ length: 6 }, () => alphabet[randomInt(alphabet.length)]).join(''); } while (rooms.has(code));
        const r = { code, players: [player(body.build), null], revision: 1, stage: 'lobby', seed: 0, matchId: 0,
          frame: null, inputs: [], created: now() }; rooms.set(code, r); result = view(r, 0, true);
      } else {
        const match = /^\/rooms\/([A-Z2-9]{6})(?:\/(join|build|ready|start|frame|input|lobby|leave))?$/.exec(path);
        if (!match) fail(404, '部屋が見つかりません。');
        const r = rooms.get(match[1]); if (!r) fail(404, '部屋が見つかりません。コードを確認してください。');
        const action = match[2];
        if (action === 'join' && req.method === 'POST') {
          if (body.version !== VERSION) fail(409, 'ゲームのバージョンをそろえてください。');
          if (r.players[1] || r.stage !== 'lobby') fail(409, 'この部屋は満員です。');
          r.players[1] = player(body.build); r.revision++; result = view(r, 1, true);
        } else {
          const token = (req.headers.authorization || '').replace(/^Bearer /, '');
          const side = r.players.findIndex(p => p && p.token === token);
          if (side < 0) fail(401, '部屋への接続が切れました。入り直してください。');
          const p = r.players[side]; p.seen = now();
          if (!action && req.method === 'GET') result = view(r, side);
          else if (req.method !== 'POST') fail(405, 'この操作は利用できません。');
          else if (action === 'build') {
            if (r.stage !== 'lobby') fail(409, '対戦中は装備を変更できません。');
            p.build = build(body.build); p.ready = false; r.revision++; result = view(r, side);
          } else if (action === 'ready') {
            if (r.stage !== 'lobby') fail(409, '対戦中です。');
            p.ready = Boolean(body.ready); r.revision++; result = view(r, side);
          } else if (action === 'start') {
            if (side !== 0) fail(403, 'ホストが対戦を開始します。');
            if (r.stage !== 'lobby' || !r.players.every(p => p?.ready)) fail(409, '2人の準備完了を待ってください。');
            r.stage = 'battle'; r.seed = randomInt(1, 2147483647); r.matchId++; r.frame = null; r.inputs = []; r.revision++; result = view(r, side);
          } else if (action === 'frame') {
            if (side !== 0) fail(403, 'ホストだけが試合を同期できます。');
            const f = body.frame;
            if (r.stage !== 'battle' || body.matchId !== r.matchId) fail(409, '試合が切り替わりました。');
            if (!f || !Number.isInteger(f.sequence) || f.sequence < 1 || !Number.isFinite(f.clock) ||
                !Array.isArray(f.actors) || f.actors.length !== 2 || !Array.isArray(f.events) || f.events.length > 128)
              fail(400, '試合データを読み取れません。');
            if (f.ended && (!f.result || ![-1, 0, 1].includes(f.result.winner) || !Array.isArray(f.result.hpRemaining) ||
                !Array.isArray(f.result.metrics) || f.result.metrics.length !== 2 || typeof f.result.finishReason !== 'string'))
              fail(400, '試合結果を読み取れません。');
            if (!r.frame || f.sequence > r.frame.sequence) r.frame = f;
            result = { ok: true };
          } else if (action === 'input') {
            // The guest's spectator actions (special / cheer / just guard); the host applies them to its simulation.
            if (side !== 1) fail(403, 'ホストの操作はホストの端末で反映します。');
            const i = body.input;
            if (r.stage !== 'battle' || body.matchId !== r.matchId) fail(409, '試合が切り替わりました。');
            if (!i || !Number.isInteger(i.sequence) || i.sequence < 1 || !inputKinds.includes(i.kind)) fail(400, '操作を読み取れません。');
            if (i.sequence > (r.inputs.at(-1)?.sequence ?? 0)) {
              r.inputs.push({ sequence: i.sequence, kind: i.kind });
              if (r.inputs.length > 16) r.inputs.shift();
            }
            result = view(r, side);
          } else if (action === 'lobby') {
            r.stage = 'lobby'; r.frame = null; r.inputs = []; for (const item of r.players) if (item) item.ready = false;
            r.revision++; result = view(r, side);
          } else if (action === 'leave') {
            if (side === 0) rooms.delete(r.code);
            else { r.players[1] = null; r.players[0].ready = false; r.stage = 'lobby'; r.frame = null; r.inputs = []; r.revision++; }
            result = { ok: true };
          } else fail(404, 'この操作は利用できません。');
        }
      }
      res.end(JSON.stringify(result));
    } catch (e) {
      res.writeHead(e.status || 500); res.end(JSON.stringify({ error: e.status ? e.message : 'サーバーでエラーが発生しました。' }));
    }
  });
  server.requestTimeout = 10000; server.headersTimeout = 10000;
  return server;
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const port = Number(process.env.PORT || 8787);
  createRoomServer().listen(port, process.env.HOST || '0.0.0.0', () => console.log(`MOJI Fighter X rooms listening on ${port}`));
}
