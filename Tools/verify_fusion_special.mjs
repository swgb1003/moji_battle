import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

// One generation request per run. Repeating the same fixture uses the server cache.
const base = (process.argv[2] || 'http://127.0.0.1:8787').replace(/\/$/, '');
const reportPath = process.argv[3], version = 'moji-fighter-x-rooms-v7';
const recipe = {version:2,name:'海織',leftSource:'紅',rightSource:'海',layout:0,leftUseSecond:false,rightUseFirst:false,
  leftWidth:1,leftHeight:1,rightWidth:1,rightHeight:1,leftX:0,leftY:0,rightX:0,rightY:0,gap:8};
const request = {version,character:'合',name:'蒼流一閃',fusionEnabled:true,fusion:recipe};
const build = {character:'合',fontType:3,weaponSize:1,gripType:4,battleStyle:0,weaponFlip:0,
  gripPosition:.5,gripPoint:{x:.2,y:.7},customGrip:true,independentOffhandGrip:true,
  offhandGripPosition:.5,offhandGripPoint:{x:.8,y:.3},offhandCustomGrip:true,
  splitMing:true,fusionEnabled:true,fusion:recipe};
async function call(path,body,token,status=200){
  const response=await fetch(base+path,{method:body?'POST':'GET',headers:{'Content-Type':'application/json',
    ...(token?{Authorization:'Bearer '+token}:{})},...(body?{body:JSON.stringify(body)}:{}),signal:AbortSignal.timeout(90000)});
  const result=await response.json();assert.equal(response.status,status,`${path}: ${response.status} ${result.error||''}`);return result;
}
let host;
try{
  const health=await call('/health');assert.equal(health.version,version);assert.equal(health.capabilities.fusionSpecials,true);
  await call('/specials',{...request,fusionEnabled:false},null,400);
  await call('/specials',{...request,name:'字'.repeat(8)},null,400);
  const made=await call('/specials',request);
  assert.equal(made.special.name,request.name);assert.equal(made.special.fusionEnabled,true);assert.deepEqual(made.special.fusion,recipe);
  assert.ok(made.special.power+made.special.speed+made.special.reach<=9);
  host=await call('/rooms',{version,build:{...build,special:made.special}});const path='/rooms/'+host.code;
  const guest=await call(path+'/join',{version,build:{...build,character:'刀',gripType:0,splitMing:false,fusionEnabled:false}});
  assert.deepEqual(guest.players[0].build.special,made.special);assert.equal(guest.players[1].build.special,null);
  await call(path+'/ready',{ready:true},host.token);await call(path+'/ready',{ready:true},guest.token);
  const match=await call(path+'/start',{},host.token);assert.equal(match.stage,'battle');
  assert.deepEqual((await call(path,null,guest.token)).players[0].build.special,made.special);
  const report={url:base,checkedAtUtc:new Date().toISOString(),result:'passed',protocol:version,specialName:made.special.name,
    checks:['fusion capability','ordinary naming rule retained','seven-character bound retained','free-name fusion generation',
      'recipe binding','stat budget','host/guest equipped special round trip','ready and battle start'],generationRequests:1};
  if(reportPath)writeFileSync(reportPath,JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}finally{if(host)await call('/rooms/'+host.code+'/leave',{},host.token);}
