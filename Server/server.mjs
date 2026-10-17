import http from 'node:http';
import { randomBytes, randomInt } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { readFileSync } from 'node:fs';
import { checkName, sanitizeSpecial, createClaudeGenerator, createFakeGenerator } from './specials.mjs';

export const VERSION = 'moji-fighter-x-rooms-v7';
const inputKinds = ['special', 'cheer'];
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
// The release copies the same curated structural table used by Unity.
const structuralRows = readFileSync(new URL('./split-kanji.csv', import.meta.url), 'utf8')
  .split(/\r?\n/).filter(line => line && !line.startsWith('#')).map(line => line.split(','));
export const SPLIT_KANJI = structuralRows.map(row => row[0]);
const leftRight = new Map(structuralRows.filter(row => row[3] === 'lr').map(row => [row[0], row]));
const allParts = new Map(structuralRows.map(row => [row[0], row]));
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
  function fusion(value, character) {
    if (value == null) return null;
    if (typeof value !== 'object' || Array.isArray(value) || ![1, 2].includes(value.version))
      fail(400, '合成文字の部品を確認してください。');
    const sources = value.version === 1 ? leftRight : allParts;
    if (!sources.has(value.leftSource) || !sources.has(value.rightSource)) fail(400, '合成文字の部品を確認してください。');
    const result = { version: value.version, name: value.name, leftSource: value.leftSource, rightSource: value.rightSource };
    if (value.version === 2) {
      if (!Number.isInteger(value.layout) || value.layout < 0 || value.layout > 2 ||
          typeof value.leftUseSecond !== 'boolean' || typeof value.rightUseFirst !== 'boolean') fail(400, '合成文字の配置を確認してください。');
      Object.assign(result, { layout: value.layout, leftUseSecond: value.leftUseSecond, rightUseFirst: value.rightUseFirst });
    } else if ((value.layout != null && value.layout !== 0) || value.leftUseSecond || value.rightUseFirst) {
      fail(400, '旧形式の合成文字の部品を確認してください。');
    }
    if (typeof result.name !== 'string' || result.name.length > 24 || [...segmenter.segment(result.name)].length > 12 || /[\u0000-\u001f\u007f-\u009f]/u.test(result.name))
      fail(400, '合成文字の名前は12文字までです。');
    for (const [key, min, max] of [
      ['leftWidth', .7, 1.3], ['leftHeight', .7, 1.3], ['rightWidth', .7, 1.3], ['rightHeight', .7, 1.3],
      ['leftX', -20, 20], ['rightX', -20, 20], ['leftY', -24, 24], ['rightY', -24, 24], ['gap', 0, 24],
    ]) {
      if (!Number.isFinite(value[key]) || value[key] < min || value[key] > max) fail(400, '合成文字の配置を確認してください。');
      result[key] = value[key];
    }
    const first = sources.get(result.leftSource)[result.leftUseSecond ? 2 : 1], second = sources.get(result.rightSource)[result.rightUseFirst ? 1 : 2];
    const layout = ['lr', 'tb', 'enc'][result.layout ?? 0];
    const identity = structuralRows.find(row => row[3] === layout && row[1] === first && row[2] === second)?.[0] ?? '合';
    if (identity !== character) fail(400, '合成文字の情報が一致しません。');
    return result;
  }
  function build(b) {
    if (!b || typeof b.character !== 'string' || b.character.length > 12 ||
        [...segmenter.segment(b.character.trim())].length !== 1) fail(400, '文字を一つ選んでください。');
    for (const [key, max] of Object.entries({ fontType: 3, weaponSize: 3, gripType: 4, battleStyle: 3, weaponFlip: 3 }))
      if (!Number.isInteger(b[key]) || b[key] < 0 || b[key] > max) fail(400, '装備の設定を確認してください。');
    for (const value of [b.gripPosition, b.gripPoint?.x, b.gripPoint?.y])
      if (!Number.isFinite(value) || value < 0 || value > 1) fail(400, '持ち手の位置を確認してください。');
    if (b.gripType === 4 && b.weaponSize > 1) fail(400, '二刀流はS・Mサイズのみです。');
    const offhandCharacter = typeof b.offhandCharacter === 'string' ? b.offhandCharacter.trim() : '';
    if (offhandCharacter && (offhandCharacter.length > 12 || [...segmenter.segment(offhandCharacter)].length !== 1))
      fail(400, '左手の文字を一つ選んでください。');
    const independentOffhandGrip = Boolean(b.independentOffhandGrip);
    const offhandGripPosition = independentOffhandGrip ? b.offhandGripPosition : b.gripPosition;
    const offhandGripPoint = independentOffhandGrip ? b.offhandGripPoint : b.gripPoint;
    for (const value of [offhandGripPosition, offhandGripPoint?.x, offhandGripPoint?.y])
      if (!Number.isFinite(value) || value < 0 || value > 1) fail(400, '左手の持ち手の位置を確認してください。');
    if (b.fusionEnabled && b.fusion == null) fail(400, '合成文字のデータがありません。');
    if (b.offhandFusionEnabled && (!offhandCharacter || b.offhandFusion == null)) fail(400, '左手の合成文字を確認してください。');
    const mainFusion = b.fusionEnabled ? fusion(b.fusion, b.character.trim()) : null;
    const offhandFusion = b.offhandFusionEnabled ? fusion(b.offhandFusion, offhandCharacter) : null;
    if (b.splitMing && !mainFusion && !SPLIT_KANJI.includes(b.character.trim())) fail(400, 'この漢字は分離に対応していません。');
    return { character: b.character.trim(), fontType: b.fontType, weaponSize: b.weaponSize, gripType: b.gripType,
      battleStyle: b.battleStyle, weaponFlip: b.weaponFlip, gripPosition: b.gripPosition,
      gripPoint: { x: b.gripPoint.x, y: b.gripPoint.y }, customGrip: Boolean(b.customGrip),
      offhandCharacter, independentOffhandGrip: true, offhandGripPosition,
      offhandGripPoint: { x: offhandGripPoint.x, y: offhandGripPoint.y },
      offhandCustomGrip: independentOffhandGrip ? Boolean(b.offhandCustomGrip) : Boolean(b.customGrip),
      splitMing: Boolean(b.splitMing), splitSwapped: Boolean(b.splitSwapped),
      fusion: mainFusion, offhandFusion,
      fusionEnabled: mainFusion != null, offhandFusionEnabled: offhandFusion != null,
      special: sanitizeSpecial(b.special, b.character.trim(), mainFusion) };
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
      stage: r.stage, arena: r.arena ?? 0, matchId: r.matchId, seed: r.seed, frame: r.frame, inputs: r.inputs,
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
      if (path === '/health' && req.method === 'GET') { res.end(JSON.stringify({ ok: true, version: VERSION, specials: Boolean(generateSpecial), capabilities: { dualGrip: true, splitMing: true, stages: true, fusion: true, fusionVersion: 2, fusionSpecials: true, fusionLayouts: ['lr', 'tb', 'enc'], splitCharacters: SPLIT_KANJI } })); return; }
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
        if (body.fusionEnabled && body.fusion == null) fail(400, '合成文字のデータがありません。');
        const recipe = body.fusionEnabled ? fusion(body.fusion, typeof body.character === 'string' ? body.character.trim() : '') : null;
        const name = checkName(body.name, body.character, recipe != null), character = body.character.trim();
        if (!generateSpecial) fail(503, 'AI の技づくりは準備中です。');
        const key = JSON.stringify([character, recipe, name]);
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
          if (!job) {
            let weaponDescription;
            if (recipe) {
              const first = allParts.get(recipe.leftSource)[recipe.leftUseSecond ? 2 : 1];
              const second = allParts.get(recipe.rightSource)[recipe.rightUseFirst ? 1 : 2];
              weaponDescription = `合成文字「${recipe.name || first + '＋' + second}」。素材: ${recipe.leftSource}の${first} ＋ ${recipe.rightSource}の${second}。配置: ${['左右', '上下', '囲み'][recipe.layout ?? 0]}。通常の文字コードを持たない創作字。`;
            }
            job = generateSpecial({ character, name, weaponDescription }); pending.set(key, job);
          }
          let raw;
          try { raw = await job; } finally { pending.delete(key); }
          if (!raw || raw.ok === false) fail(422, (typeof raw?.reason === 'string' && raw.reason.trim().slice(0, 60)) || 'この技名では作れませんでした。');
          made = sanitizeSpecial({ ...raw, name, ...(recipe ? { fusionEnabled: true, fusion: recipe } : {}) }, character, recipe);
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
        const r = { code, players: [player(body.build), null], revision: 1, stage: 'lobby', arena: 0, seed: 0, matchId: 0,
          frame: null, inputs: [], created: now() }; rooms.set(code, r); result = view(r, 0, true);
      } else {
        const match = /^\/rooms\/([A-Z2-9]{6})(?:\/(join|build|ready|arena|start|frame|input|lobby|leave))?$/.exec(path);
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
          } else if (action === 'arena') {
            if (side !== 0) fail(403, 'ステージはホストが選びます。');
            if (r.stage !== 'lobby') fail(409, '対戦中はステージを変更できません。');
            if (!Number.isInteger(body.arena) || body.arena < 0 || body.arena > 5) fail(400, 'ステージを選び直してください。');
            if (r.arena !== body.arena) {
              r.arena = body.arena;
              for (const item of r.players) if (item) item.ready = false;
              r.revision++;
            }
            result = view(r, side);
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
