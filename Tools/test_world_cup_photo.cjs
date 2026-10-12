const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
(async () => {
  const heap = new Uint8Array([0, 0, 137, 80, 78, 71, 13, 10, 26, 10, 0]);
  const expected = heap.slice(2, 10);
  let blob, clicked = false, removed = false, revoked = false, timer;
  const link = { click(){clicked = true;}, remove(){removed = true;} };
  const library = {};
  const context = {
    LibraryManager: { library }, mergeInto: Object.assign, HEAPU8: heap, Blob,
    UTF8ToString: pointer => {assert.equal(pointer, 99);return 'moji-world-cup-test.png';},
    URL: { createObjectURL(value){blob = value;return 'blob:photo-test';}, revokeObjectURL(url){assert.equal(url, 'blob:photo-test');revoked = true;} },
    document: {createElement(tag){assert.equal(tag, 'a');return link;}, body:{appendChild(node){assert.equal(node, link);}}},
    setTimeout(fn, delay){assert.equal(delay, 60000);timer = fn;}
  };
  vm.runInNewContext(fs.readFileSync('Assets/Plugins/WebGL/MojiWorldCupPhoto.jslib', 'utf8'), context);
  library.MojiSaveWorldCupPhoto(2, 8, 99);
  heap.fill(0);
  assert.deepEqual(new Uint8Array(await blob.arrayBuffer()), expected);
  assert.equal(blob.type, 'image/png');assert.equal(link.download, 'moji-world-cup-test.png');
  assert.equal(link.href, 'blob:photo-test');assert.ok(clicked && removed);timer();assert.ok(revoked);
  fs.writeFileSync('Reports/world-cup-photo-download.json', JSON.stringify({passed:true, checks:['PNG bytes copied before Wasm allocation is released','image/png MIME','download filename','anchor click and removal','deferred Blob URL revocation'],browserAutomation:false}, null, 2));
  console.log('Photo download bridge: 5 checks passed.');
})().catch(error => {console.error(error);process.exitCode=1;});
