import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

const base = (process.argv[2] || 'http://127.0.0.1:8787').replace(/\/$/, '');
const reportPath = process.argv[3];
const version = 'moji-fighter-x-rooms-v7';
const character = process.argv[4] || '明';
const build = { character, fontType: 0, weaponSize: 1, gripType: 4, battleStyle: 0,
  weaponFlip: 3, gripPosition: .5, customGrip: true, gripPoint: {x: .15, y: .8},
  independentOffhandGrip: true, offhandGripPosition: .5, offhandCustomGrip: true,
  offhandGripPoint: {x: .85, y: .2}, offhandCharacter: '', splitMing: true, splitSwapped: true };
async function call(path, body, token) {
  const response = await fetch(base + path, { method: body ? 'POST' : 'GET',
    headers: {'Content-Type':'application/json', ...(token ? {Authorization: 'Bearer ' + token} : {})},
    ...(body ? {body: JSON.stringify(body)} : {}), signal: AbortSignal.timeout(90000) });
  const data = await response.json();
  assert.equal(response.status, 200, `${path}: ${response.status} ${data.error || ''}`);
  return data;
}
let host;
try {
  const health = await call('/health');
  assert.equal(health.capabilities?.dualGrip, true); assert.equal(health.capabilities?.splitMing, true);
  if (character !== '明') assert.ok(health.capabilities?.splitCharacters?.includes(character));
  host = await call('/rooms', {version, build});
  const path = '/rooms/' + host.code;
  const guest = await call(path + '/join', {version, build: {...build, character:'刀', offhandCharacter:'月', splitMing:false}});
  const shared = guest.players[0].build;
  assert.equal(shared.character, character);
  assert.equal(shared.splitMing, true); assert.equal(shared.splitSwapped, true);
  assert.deepEqual(shared.gripPoint, build.gripPoint); assert.deepEqual(shared.offhandGripPoint, build.offhandGripPoint);
  const edited = {...build, offhandGripPoint:{x:.3,y:.7}};
  await call(path + '/build', {build:edited}, host.token);
  const polled = await call(path, null, guest.token);
  assert.deepEqual(polled.players[0].build.offhandGripPoint, edited.offhandGripPoint);
  assert.equal(polled.players[1].build.offhandCharacter, '月');
  await call(path + '/ready', {ready:true}, host.token);
  await call(path + '/ready', {ready:true}, guest.token);
  const match = await call(path + '/start', {}, host.token);
  assert.equal(match.stage, 'battle');
  const remote = await call(path, null, guest.token);
  assert.equal(remote.seed, match.seed); assert.deepEqual(remote.players[0].build, match.players[0].build);
  const report = {url:base, checkedAtUtc:new Date().toISOString(), result:'passed',
    character, checks:['capabilities','split and swapped parts','independent grip sharing','normal offhand character',
      'equipment changes','ready and battle start','matching host and guest builds'], protocol:version};
  if (reportPath) writeFileSync(reportPath, JSON.stringify(report,null,2));
  console.log(JSON.stringify(report,null,2));
} finally {
  if (host) await call('/rooms/' + host.code + '/leave', {}, host.token);
}
