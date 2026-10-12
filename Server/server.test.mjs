import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createRoomServer, VERSION } from './server.mjs';

const build = (character = '力') => ({ character, fontType: 1, weaponSize: 1, gripType: 0, battleStyle: 0,
  weaponFlip: 0, gripPosition: .5, customGrip: false, gripPoint: { x: .5, y: .5 } });
async function setup(t, options) {
  const server = createRoomServer(options);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => { server.closeAllConnections(); server.close(resolve); }));
  const url = `http://127.0.0.1:${server.address().port}`;
  return async (path, body, token) => {
    const response = await fetch(url + path, { method: body ? 'POST' : 'GET', headers: {
      'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      ...(body ? { body: JSON.stringify(body) } : {}) });
    return { status: response.status, ...await response.json() };
  };
}
test('two friends share code, loadouts and ready state; only host starts', async t => {
  const call = await setup(t);
  const host = await call('/rooms', { version: VERSION, build: build() });
  assert.equal(host.status, 200); assert.match(host.code, /^[A-HJ-NP-Z2-9]{6}$/); assert.equal(host.side, 0);
  const path = `/rooms/${host.code}`;
  assert.equal((await call(path)).status, 401);
  const guest = await call(path + '/join', { version: VERSION, build: build('史') });
  assert.equal(guest.side, 1); assert.notEqual(guest.token, host.token);
  assert.equal((await call(path + '/join', { version: VERSION, build: build() })).status, 409);
  assert.equal((await call(path + '/start', {}, host.token)).status, 409);
  await call(path + '/ready', { ready: true }, host.token);
  await call(path + '/ready', { ready: true }, guest.token);
  assert.equal((await call(path + '/start', {}, guest.token)).status, 403);
  const battle = await call(path + '/start', {}, host.token);
  assert.equal(battle.stage, 'battle'); assert.ok(battle.seed > 0); assert.equal(battle.matchId, 1);
  const remote = await call(path, null, guest.token);
  assert.equal(remote.seed, battle.seed); assert.equal(remote.players[0].build.character, '力');
  assert.equal(remote.players[1].build.character, '史'); assert.equal(remote.token, '');
  assert.equal(JSON.stringify(remote).includes(host.token), false);
  assert.equal((await call(path + '/build', { build: build('木') }, guest.token)).status, 409);
});
test('changing equipment revokes own ready; returning to lobby revokes both', async t => {
  const call = await setup(t), host = await call('/rooms', { version: VERSION, build: build() }), path = `/rooms/${host.code}`;
  const guest = await call(path + '/join', { version: VERSION, build: build('史') });
  await call(path + '/ready', { ready: true }, host.token); await call(path + '/ready', { ready: true }, guest.token);
  const edited = await call(path + '/build', { build: build('木') }, guest.token);
  assert.equal(edited.players[1].ready, false); assert.equal(edited.players[0].ready, true);
  await call(path + '/ready', { ready: true }, guest.token); await call(path + '/start', {}, host.token);
  const lobby = await call(path + '/lobby', {}, guest.token);
  assert.equal(lobby.stage, 'lobby'); assert.equal(lobby.players.every(p => !p.ready), true);
});
test('host frames and final result are monotonic and cannot be forged by guest or stale match', async t => {
  const call = await setup(t), host = await call('/rooms', { version: VERSION, build: build() }), path = `/rooms/${host.code}`;
  const guest = await call(path + '/join', { version: VERSION, build: build('史') });
  await call(path + '/ready', { ready: true }, host.token); await call(path + '/ready', { ready: true }, guest.token);
  const match = await call(path + '/start', {}, host.token);
  const frame = { sequence: 10, clock: 42, actors: [{ hp: 0 }, { hp: 40 }], events: [], ended: true,
    result: { winner: 1, hpRemaining: [0, 40], metrics: [{ damageDealt: 60 }, { damageDealt: 100 }], finishReason: 'KO' } };
  assert.equal((await call(path + '/frame', { matchId: match.matchId, frame }, guest.token)).status, 403);
  assert.equal((await call(path + '/frame', { matchId: 0, frame }, host.token)).status, 409);
  assert.equal((await call(path + '/frame', { matchId: match.matchId, frame }, host.token)).status, 200);
  await call(path + '/frame', { matchId: match.matchId, frame: { ...frame, sequence: 9 } }, host.token);
  const remote = await call(path, null, guest.token);
  assert.equal(remote.frame.sequence, 10); assert.equal(remote.frame.result.winner, 1);
  const invalid = { ...frame, sequence: 11, result: { winner: 9 } };
  assert.equal((await call(path + '/frame', { matchId: match.matchId, frame: invalid }, host.token)).status, 400);
});
test('guest leave opens slot and cancels battle; host leave invalidates code', async t => {
  const call = await setup(t), host = await call('/rooms', { version: VERSION, build: build() }), path = `/rooms/${host.code}`;
  const guest = await call(path + '/join', { version: VERSION, build: build('史') });
  await call(path + '/leave', {}, guest.token);
  const state = await call(path, null, host.token);
  assert.equal(state.players[1].joined, false); assert.equal(state.stage, 'lobby');
  assert.equal((await call(path, null, guest.token)).status, 401);
  assert.equal((await call(path + '/join', { version: VERSION, build: build('木') })).status, 200);
  await call(path + '/leave', {}, host.token);
  assert.equal((await call(path, null, host.token)).status, 404);
});
test('disconnect expiry removes guest and deletes abandoned host rooms', async t => {
  let clock = 1000;
  const call = await setup(t, { now: () => clock, grace: 100 });
  const host = await call('/rooms', { version: VERSION, build: build() }), path = `/rooms/${host.code}`;
  await call(path + '/join', { version: VERSION, build: build('史') });
  clock += 70; await call(path, null, host.token); clock += 50;
  const state = await call(path, null, host.token); assert.equal(state.players[1].joined, false);
  clock += 101; assert.equal((await call(path, null, host.token)).status, 404);
});
test('invalid builds, incompatible versions and room limits produce actionable errors', async t => {
  const call = await setup(t, { maxRooms: 1 });
  assert.equal((await call('/rooms', { version: 'old', build: build() })).status, 409);
  assert.equal((await call('/rooms', { version: VERSION, build: build('ab') })).status, 400);
  assert.equal((await call('/rooms', { version: VERSION, build: { ...build(), gripPoint: { x: -1, y: .5 } } })).status, 400);
  assert.equal((await call('/rooms', { version: VERSION, build: build() })).status, 200);
  assert.equal((await call('/rooms', { version: VERSION, build: build() })).status, 503);
});
test('only the guest posts spectator actions; host reads them in order for the current match', async t => {
  const call = await setup(t);
  const host = await call('/rooms', { version: VERSION, build: build() });
  const path = `/rooms/${host.code}`;
  const guest = await call(path + '/join', { version: VERSION, build: build('史') });
  const input = (sequence, kind, token, matchId = 1) => call(path + '/input', { matchId, input: { sequence, kind } }, token);
  assert.equal((await input(1, 'cheer', guest.token)).status, 409); // not in battle yet
  await call(path + '/ready', { ready: true }, host.token);
  await call(path + '/ready', { ready: true }, guest.token);
  await call(path + '/start', {}, host.token);
  assert.equal((await input(1, 'special', host.token)).status, 403);
  assert.equal((await input(1, 'fly', guest.token)).status, 400);
  assert.equal((await input(1, 'guard', guest.token)).status, 400); // just guard was removed
  assert.equal((await input(1, 'cheer', guest.token, 2)).status, 409);
  assert.equal((await input(1, 'cheer', guest.token)).status, 200);
  await input(2, 'cheer', guest.token);
  await input(2, 'special', guest.token); // duplicate sequence is ignored
  for (let s = 3; s <= 20; s++) await input(s, 'cheer', guest.token);
  const seen = await call(path, null, host.token);
  assert.equal(seen.inputs.length, 16); assert.equal(seen.inputs.at(-1).sequence, 20);
  assert.equal(seen.inputs[0].sequence, 5);
  await call(path + '/lobby', {}, host.token);
  assert.deepEqual((await call(path, null, host.token)).inputs, []);
});
test('behind a trusted proxy, rate limits apply per forwarded client', async t => {
  process.env.TRUST_PROXY = '1'; t.after(() => { delete process.env.TRUST_PROXY; });
  const server = createRoomServer();
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => { server.closeAllConnections(); server.close(resolve); }));
  const url = `http://127.0.0.1:${server.address().port}/health`;
  const get = ip => fetch(url, { headers: { 'X-Forwarded-For': `${ip}, 10.0.0.1` } }).then(r => r.status);
  for (let i = 0; i < 600; i++) await get('203.0.113.1');
  assert.equal(await get('203.0.113.1'), 429);
  assert.equal(await get('203.0.113.2'), 200);
});

const designed = { ok: true, reason: '', reading: 'えんりゅういっせん', steps: ['Launch', 'StrokeShot', 'Teleport'], power: 5, speed: 5, reach: 5,
  element: 'fire', effect: 'pillar', primary: '#ff3300', secondary: 'red', callout: '燃え尽きろ！', description: '炎の龍が昇る。' };
test('special moves: name rules, AI design is clamped to the stat budget, cached, and rate limited', async t => {
  let calls = 0;
  const call = await setup(t, { specialsPerHour: 3, generateSpecial: async ({ character, name }) => {
    calls++; assert.equal(character, '炎'); return name === '炎上商法' ? { ok: false, reason: '公開に向かない名前です。' } : designed; } });
  const make = (name, character = '炎', version = VERSION) => call('/specials', { version, character, name });
  assert.equal((await make('炎龍一閃', '炎', 'old')).status, 409);
  assert.equal((await make('龍一閃')).status, 400); // must contain the chosen character
  assert.equal((await make('炎炎炎炎炎炎炎炎')).status, 400); // 8 characters
  assert.equal(calls, 0);
  const made = await make('炎龍一閃');
  assert.equal(made.status, 200);
  const s = made.special;
  assert.equal(s.name, '炎龍一閃'); assert.equal(s.character, '炎');
  assert.deepEqual(s.steps, ['Launch', 'StrokeShot']); // unknown motion dropped
  assert.ok(s.power + s.speed + s.reach <= 9 && Math.max(s.power, s.speed, s.reach) <= 5);
  assert.equal(s.primary, '#FF3300'); assert.equal(s.secondary, '#F6B23C'); // invalid colour falls back to the element's
  assert.equal((await make('炎龍一閃')).special.name, '炎龍一閃');
  assert.equal(calls, 1); // same name served from cache
  const refused = await make('炎上商法');
  assert.equal(refused.status, 422); assert.equal(refused.error, '公開に向かない名前です。');
  await make('炎の拳');
  assert.equal((await make('炎の蹴り')).status, 429);
  assert.equal(calls, 3);
});
test('special generation is unavailable without an AI key; builds carry a sanitized special', async t => {
  const call = await setup(t, { generateSpecial: null });
  assert.equal((await call('/health')).specials, false);
  assert.equal((await call('/specials', { version: VERSION, character: '力', name: '力技' })).status, 503);
  const special = { ...designed, name: '怪力乱神', steps: ['Whirl'], power: 9, speed: 9, reach: 9 };
  const host = await call('/rooms', { version: VERSION, build: { ...build(), special } });
  assert.deepEqual(host.players[0].build.special.steps, ['Whirl']);
  assert.ok(host.players[0].build.special.power <= 5);
  const other = await call('/rooms', { version: VERSION, build: { ...build(), special: { ...special, name: '別の技' } } });
  assert.equal(other.players[0].build.special, null); // a special for another character is dropped
});
test('the Claude request asks for the schema-shaped design and reads it back', async () => {
  const { createClaudeGenerator } = await import('./specials.mjs');
  let sent, headers;
  const generate = createClaudeGenerator({ apiKey: 'test-key', fetch: async (url, init) => {
    sent = JSON.parse(init.body); headers = new Headers(init.headers);
    return new Response(JSON.stringify({ id: 'msg_1', type: 'message', role: 'assistant', model: sent.model, stop_reason: 'end_turn',
      content: [{ type: 'text', text: JSON.stringify(designed) }], usage: { input_tokens: 1, output_tokens: 1 } }),
      { status: 200, headers: { 'content-type': 'application/json' } });
  } });
  const raw = await generate({ character: '炎', name: '炎龍一閃' });
  assert.equal(raw.element, 'fire');
  assert.equal(sent.model, 'claude-opus-5-5'); assert.equal(sent.fallbacks, 'default');
  assert.equal(sent.output_config.format.type, 'json_schema'); assert.equal(sent.output_config.effort, 'low');
  assert.match(sent.messages[0].content, /炎龍一閃/);
  assert.match(headers.get('anthropic-beta'), /server-side-fallback-2026-07-01/);
});
test('CORS allows the production site and its Vercel deployment URLs only', async () => {
  const { allowOrigin } = await import('./server.mjs');
  const setting = 'https://moji-fighter-x.vercel.app,https://moji-fighter-*-swgb1003-5441s-projects.vercel.app';
  assert.equal(allowOrigin(setting, 'https://moji-fighter-x.vercel.app'), 'https://moji-fighter-x.vercel.app');
  assert.equal(allowOrigin(setting, 'https://moji-fighter-fuibyhaby-swgb1003-5441s-projects.vercel.app'),
    'https://moji-fighter-fuibyhaby-swgb1003-5441s-projects.vercel.app');
  assert.equal(allowOrigin(setting, 'https://evil.example'), 'https://moji-fighter-x.vercel.app');
  assert.equal(allowOrigin(setting, 'https://moji-fighter-a.b-swgb1003-5441s-projects.vercel.app'), 'https://moji-fighter-x.vercel.app');
  assert.equal(allowOrigin(undefined, 'https://x.example'), '*');
});
