const assert = require('node:assert/strict');
module.exports = async (base, token, ownerKey) => {
  const root = base + 'androidremote/owner/';
  const headers = { Authorization: 'Bearer ' + token, 'X-ControlHub-Owner': ownerKey, 'Content-Type': 'application/json' };
  const request = (path = '', method = 'GET', body, h = headers) => fetch(root + path,
    { method, headers: h, ...(body === undefined ? {} : { body }) });
  const args = JSON.stringify({name: 'Smoke phone', permissions: ['status.read', 'obs.scenes']});
  for (const [path, method, body] of [['', 'GET'], ['pairings','POST',args], ['access','DELETE']]) {
    assert.equal((await request(path,method,body,{'X-ControlHub-Owner':ownerKey})).status,401);
    assert.equal((await request(path,method,body,{Authorization:'Bearer '+token})).status,403);
    assert.equal((await request(path,method,body,{...headers,'X-ControlHub-Owner':token})).status,403);
    assert.equal((await request(path,method,body,{...headers,Origin:'https://example.com'})).status,403);
  }
  for (const bad of ['null', '{', '{"name":"a","name":"b","permissions":["status.read"]}',
    '{"name":"a","permissions":[]}', '{"name":"a","permissions":["*"]}', '{"name":"a","permissions":[1]}',
    '{"name":"a","permissions":["status.read"],"token":"x"}']) {
    assert.equal((await request('pairings','POST',bad)).status,400);
  }
  assert.equal((await (await request()).json()).pendingPairings.length,0);
  const response = await request('pairings','POST',args);
  assert.equal(response.status,201);
  assert.equal(response.headers.get('cache-control'),'no-store');
  const offer = await response.json();
  assert.ok(typeof offer.secret === 'string' && /^[A-F0-9]{64}$/.test(offer.secret));
  const listed = await request();
  assert.equal(listed.headers.get('cache-control'),'no-store');
  const raw = await listed.text();
  assert.ok(!raw.includes(offer.secret) && !raw.includes(ownerKey) && !raw.includes(token));
  const view = JSON.parse(raw);
  assert.equal(view.transportReady,false);
  assert.equal(view.devices.length,0);
  assert.equal(view.pendingPairings[0].id,offer.id);
  assert.deepEqual(view.pendingPairings[0].permissions,['obs.scenes','status.read']);
  assert.equal((await request('devices/'+offer.id,'DELETE')).status,404);
  assert.equal((await request('pairings/'+offer.id,'DELETE')).status,204);
  assert.equal((await request('pairings/'+offer.id,'DELETE')).status,404);
  for (let i=0;i<8;i++) assert.equal((await request('pairings','POST',args)).status,201);
  assert.equal((await request('pairings','POST',args)).status,409);
  assert.equal((await request('access','DELETE')).status,204);
  assert.equal((await (await request()).json()).pendingPairings.length,0);
  assert.equal((await fetch(base+'androidremote/pair',{method:'POST',headers,body:args})).status,404);
};
