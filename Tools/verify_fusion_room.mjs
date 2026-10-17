import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

const base = (process.argv[2] || 'http://127.0.0.1:8787').replace(/\/$/, '');
const reportPath = process.argv[3];
const version = 'moji-fighter-x-rooms-v7';
const recipe = {version:1, name:'月樹', leftSource:'林', rightSource:'明', leftWidth:1.2, leftHeight:.9,
  rightWidth:1.1, rightHeight:1, leftX:-8, leftY:12, rightX:4, rightY:-10, gap:12};
const build = {character:'合', fontType:3, weaponSize:1, gripType:4, battleStyle:0, weaponFlip:3,
  gripPosition:.5, customGrip:true, gripPoint:{x:.2,y:.7}, independentOffhandGrip:true,
  offhandCustomGrip:true, offhandGripPosition:.5, offhandGripPoint:{x:.8,y:.3}, offhandCharacter:'',
  splitMing:true, splitSwapped:true, fusionEnabled:true, offhandFusionEnabled:false, fusion:recipe, offhandFusion:null};
async function call(path, body, token) {
  const response = await fetch(base + path, {method:body?'POST':'GET', headers:{'Content-Type':'application/json',
    ...(token?{Authorization:'Bearer '+token}:{})}, ...(body?{body:JSON.stringify(body)}:{}), signal:AbortSignal.timeout(90000)});
  const result = await response.json(); assert.equal(response.status,200,`${path}: ${response.status} ${result.error||''}`); return result;
}
let host;
try {
  const health = await call('/health'); assert.equal(health.version,version); assert.equal(health.capabilities.fusionVersion,2);
  host = await call('/rooms',{version,build}); const path='/rooms/'+host.code;
  const guest = await call(path+'/join',{version,build:{...build, character:'刀', offhandCharacter:'合', fusionEnabled:false, offhandFusionEnabled:true, fusion:null, offhandFusion:recipe, splitMing:false}});
  assert.deepEqual(guest.players[0].build.fusion,recipe); assert.deepEqual(guest.players[1].build.offhandFusion,recipe);
  assert.equal(guest.players[0].build.splitMing,true); assert.equal(guest.players[0].build.splitSwapped,true);
  assert.deepEqual(guest.players[0].build.offhandGripPoint,build.offhandGripPoint);
  const changed={...build,fusion:{...recipe,name:'月樹 改',gap:18,rightY:14}};
  await call(path+'/build',{build:changed},host.token); const polled=await call(path,null,guest.token);
  assert.deepEqual(polled.players[0].build.fusion,changed.fusion);
  const vertical = {...recipe,version:2,name:'心と雨',leftSource:'思',rightSource:'雪',layout:1,leftUseSecond:true,rightUseFirst:true};
  const enclosure = {...recipe,version:2,name:'心の箱',leftSource:'国',rightSource:'思',layout:2,leftUseSecond:false,rightUseFirst:false};
  const expanded={...build,character:'合',fusion:vertical,offhandCharacter:'合',offhandFusionEnabled:true,offhandFusion:enclosure,splitMing:false};
  await call(path+'/build',{build:expanded},host.token);
  const shared=await call(path,null,guest.token);
  assert.deepEqual(shared.players[0].build.fusion,vertical); assert.deepEqual(shared.players[0].build.offhandFusion,enclosure);
  await call(path+'/ready',{ready:true},host.token); await call(path+'/ready',{ready:true},guest.token);
  const match=await call(path+'/start',{},host.token); assert.equal(match.stage,'battle');
  const remote=await call(path,null,guest.token); assert.equal(remote.seed,match.seed); assert.deepEqual(remote.players,match.players);
  const report={url:base,checkedAtUtc:new Date().toISOString(),result:'passed',protocol:version,
    checks:['fusion v2 capability','legacy v1 recipe round trip','whole composite in offhand','resplit and swapped parts',
      'top-bottom and enclosure sources','both source component selections','v2 layout and offhand recipe round trip',
      'independent grips','recipe edit','ready/start','identical host and guest recipes and seed']};
  if(reportPath)writeFileSync(reportPath,JSON.stringify(report,null,2)); console.log(JSON.stringify(report,null,2));
}finally{if(host)await call('/rooms/'+host.code+'/leave',{},host.token);}
