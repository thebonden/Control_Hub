const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const pluginDir = path.join(__dirname, "../Plugins/StreamDeck/dk.thebonden.controlhub.sdPlugin");
const { makeCommand, statusTitle, definitions } = require(path.join(pluginDir, "actions.js"));
const { createBridge } = require(path.join(pluginDir, "bridge.js"));
const prefix = "dk.thebonden.controlhub.";
const token = "x".repeat(40);
const appear = (action, settings = {}) => ({ event: "willAppear", context: "key", action: prefix + action, payload: { settings: { token, ...settings } } });

test("all manifest actions map to implementations and assets exist", () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(pluginDir, "manifest.json")));
  assert.equal(manifest.Actions.length, Object.keys(definitions).length);
  for (const file of [manifest.CodePath, manifest.PropertyInspectorPath]) assert.ok(fs.existsSync(path.join(pluginDir, file)));
  for (const action of manifest.Actions) {
    assert.ok(definitions[action.UUID.slice(prefix.length)]);
    for (const file of [action.Icon, action.States[0].Image]) assert.ok(fs.existsSync(path.join(pluginDir, file + ".svg")));
  }
});
test("PowerPoint and OBS actions produce versioned commands", () => {
  assert.equal(makeCommand(prefix+"next", {}, "id").action, "next");
  assert.deepEqual(makeCommand(prefix+"scene", { sceneName: "Camera" }, "id").arguments, { sceneName: "Camera" });
  assert.deepEqual(makeCommand(prefix+"mute", { inputName: "Mic", muted: false }, "id").arguments, { inputName: "Mic", muted: false });
  assert.notEqual(makeCommand(prefix+"next", {}).id, makeCommand(prefix+"next", {}).id);
});
test("invalid settings never become commands", () => {
  assert.throws(() => makeCommand(prefix+"goto", { slide: 1.5 }));
  assert.throws(() => makeCommand(prefix+"goto", { slide: "" }));
  assert.throws(() => makeCommand(prefix+"scene", {}));
  assert.throws(() => makeCommand(prefix+"mute", { inputName: "Mic", muted: "false" }));
  assert.throws(() => makeCommand("other.next", {}));
  assert.throws(() => makeCommand(prefix+"status", {}));
});
test("keyDown dispatches once; keyUp does not dispatch", async () => {
  const sent = [], calls = [];
  const bridge = createBridge(m => sent.push(m), async (url, options) => {
    calls.push({ url, options }); const cmd = JSON.parse(options.body);
    return { ok: true, json: async () => ({ protocolVersion: "1", id: cmd.id, success: true }) };
  });
  await bridge.handle(appear("next"));
  await bridge.handle({ event: "keyDown", context: "key" });
  await bridge.handle({ event: "keyUp", context: "key" });
  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, "http://127.0.0.1:5187/api/v1/commands");
  assert.equal(calls[0].options.headers.Authorization, "Bearer " + token);
  assert.ok(sent.some(m => m.event === "showOk"));
});
test("overlapping presses are suppressed and failures are never retried", async () => {
  let release, calls = 0; const sent = [];
  const bridge = createBridge(m => sent.push(m), () => { calls++; return new Promise(resolve => { release = resolve; }); });
  await bridge.handle(appear("recordstart"));
  const first = bridge.handle({ event: "keyDown", context: "key" });
  await bridge.handle({ event: "keyDown", context: "key" });
  release({ ok: false });
  await first;
  assert.equal(calls, 1);
  assert.ok(sent.some(m => m.event === "showAlert"));
  assert.ok(!sent.some(m => m.event === "showOk"));
});
test("mismatched command response cannot show success", async () => {
  const sent = [];
  const bridge = createBridge(m => sent.push(m), async () => ({ ok: true, json: async () => ({ protocolVersion:"1", id:"wrong", success:true }) }));
  await bridge.handle(appear("next"));
  await bridge.handle({ event:"keyDown", context:"key" });
  assert.ok(sent.some(m => m.event === "showAlert"));
});
test("status distinguishes unavailable devices and stopped outputs", () => {
  const status = { protocolVersion:"1", plugins:[{ id:"powerpoint",state:"ready",data:{position:4} },
    { id:"obs",state:"ready",data:{recording:{outputActive:false},streaming:{outputActive:true}} }] };
  assert.equal(statusTitle(status,"en"), "PPT 4\nREC OFF\nLIVE ON");
  status.plugins[1].state = "unavailable";
  assert.equal(statusTitle(status,"da"), "PPT 4\nOPT ?\nLIVE ?");
});
test("missing token does not contact the host", async () => {
  let calls = 0; const bridge = createBridge(() => {}, async () => { calls++; });
  await bridge.handle(appear("next", { token:"" }));
  await bridge.handle({ event:"keyDown",context:"key" });
  assert.equal(calls,0);
});
test("hidden status keys stop polling", async () => {
  let calls = 0;
  const bridge = createBridge(() => {}, async () => { calls++; return {ok:true,json:async()=>({protocolVersion:"1",plugins:[]})}; });
  await bridge.handle(appear("status"));
  await bridge.handle({event:"willDisappear",context:"key"});
  await bridge.poll();
  assert.equal(calls,1);
});

const { connectionReport } = require(path.join(pluginDir, "diagnostics.js"));
const statusFixture = () => ({ protocolVersion:"1",version:"0.0.001",plugins:[
  {id:"powerpoint",state:"ready"},{id:"obs",state:"ready"}] });
const checkMessage = (extra = {}) => ({event:"sendToPlugin",context:"key",action:prefix+"next",
  payload:{kind:"checkConnection",requestId:"check-1",token,...extra}});
test("main plugin icons are PNGs at both required resolutions", () => {
  for(const [name,size] of [["plugin",28],["plugin@2x",56]]) {
    const png=fs.readFileSync(path.join(pluginDir,"assets",name+".png"));
    assert.equal(png.subarray(0,8).toString("hex"),"89504e470d0a1a0a");
    assert.equal(png.readUInt32BE(16),size);
    assert.equal(png.readUInt32BE(20),size);
  }
});
test("connection check only reads status and does not persist or echo a token", async () => {
  const sent=[],calls=[];
  const bridge=createBridge(m=>sent.push(m),async(url,options)=>{
    calls.push({url,options});return {ok:true,json:async()=>statusFixture()};
  });
  await bridge.handle(appear("next"));
  await bridge.handle(checkMessage());
  assert.equal(calls.length,1);
  assert.ok(calls[0].url.endsWith("/status"));
  assert.equal(calls[0].options.method,"GET");
  assert.equal(calls[0].options.body,undefined);
  assert.deepEqual(sent.map(m=>m.event),["sendToPropertyInspector"]);
  assert.equal(sent[0].payload.code,"ready");
  assert.equal(sent[0].payload.requestId,"check-1");
  assert.ok(!JSON.stringify(sent).includes(token));
});
test("connection check distinguishes wrong token and busy host", async () => {
  for(const [status,code] of [[401,"unauthorized"],[429,"busy"]]) {
    const sent=[];
    const bridge=createBridge(m=>sent.push(m),async()=>({ok:false,status,json:async()=>{throw new Error("must not read error body");}}));
    await bridge.handle(appear("next"));
    await bridge.handle(checkMessage());
    assert.equal(sent[0].payload.code,code);
  }
});
test("connection check rejects missing token before networking", async () => {
  const sent=[];let calls=0;
  const bridge=createBridge(m=>sent.push(m),async()=>{calls++;});
  await bridge.handle(appear("next"));
  await bridge.handle(checkMessage({token:""}));
  assert.equal(calls,0);
  assert.equal(sent[0].payload.code,"invalid_token");
});
test("connection check sanitizes transport exceptions", async () => {
  const sent=[];
  const bridge=createBridge(m=>sent.push(m),async()=>{throw new Error(token);});
  await bridge.handle(appear("next"));
  await bridge.handle(checkMessage());
  assert.equal(sent[0].payload.code,"unreachable");
  assert.ok(!JSON.stringify(sent).includes(token));
});
test("connection report distinguishes unavailable programs from a stopped host", () => {
  const status=statusFixture();status.plugins[1].state="unavailable";
  const report=connectionReport(status);
  assert.equal(report.code,"partial");
  assert.equal(report.checks[0].state,"ready");
  assert.equal(report.checks[1].state,"unavailable");
  assert.ok(report.message.da && report.message.en);
});
test("malformed or duplicate status cannot claim connectivity", () => {
  assert.equal(connectionReport({}).code,"invalid_response");
  const status=statusFixture();status.plugins.push(status.plugins[0]);
  assert.equal(connectionReport(status).code,"invalid_response");
  assert.equal(connectionReport({...statusFixture(),protocolVersion:"2"}).code,"invalid_response");
});
test("unexpected inspector messages do not perform requests", async () => {
  let calls=0;
  const bridge=createBridge(()=>{},async()=>{calls++;});
  await bridge.handle(appear("next"));
  await bridge.handle({...checkMessage(),action:prefix+"streamstart"});
  await bridge.handle(checkMessage({kind:"execute",action:"stream.start"}));
  assert.equal(calls,0);
});
test("late success cannot update a newly configured key", async () => {
  const sent=[];let release;
  const bridge=createBridge(m=>sent.push(m),async(url,options)=>new Promise(resolve=>{
    release=()=>resolve({ok:true,json:async()=>({protocolVersion:"1",id:JSON.parse(options.body).id,success:true})});
  }));
  await bridge.handle(appear("next"));
  const first=bridge.handle({event:"keyDown",context:"key"});
  await bridge.handle({...appear("scene",{sceneName:"Other"}),event:"didReceiveSettings"});
  release();await first;
  assert.equal(sent.length,0);
});
test("late failure cannot update a reused key context", async () => {
  const sent=[];let release;
  const bridge=createBridge(m=>sent.push(m),()=>new Promise(resolve=>{release=resolve;}));
  await bridge.handle(appear("next"));
  const first=bridge.handle({event:"keyDown",context:"key"});
  await bridge.handle({event:"willDisappear",context:"key"});
  await bridge.handle(appear("recordstart"));
  release({ok:false});await first;
  assert.equal(sent.length,0);
});
test("overlapping connection checks issue one request", async () => {
  let calls=0,release;const sent=[];
  const bridge=createBridge(m=>sent.push(m),()=>{calls++;return new Promise(resolve=>{release=resolve;});});
  await bridge.handle(appear("next"));
  const first=bridge.handle(checkMessage());
  await bridge.handle(checkMessage({requestId:"check-2"}));
  release({ok:true,json:async()=>statusFixture()});await first;
  assert.equal(calls,1);
  assert.equal(sent[0].payload.requestId,"check-1");
});
