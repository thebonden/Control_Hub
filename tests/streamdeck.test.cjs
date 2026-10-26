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
