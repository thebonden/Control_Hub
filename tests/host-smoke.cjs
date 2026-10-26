const assert = require("node:assert/strict");
const { spawn } = require("node:child_process");
const { randomBytes, randomUUID } = require("node:crypto");
const token = randomBytes(32).toString("hex");
const child = spawn("dotnet", ["src/ControlHub.Host/bin/Release/net10.0/ControlHub.Host.dll"],
  { env: { ...process.env, CONTROLHUB_TOKEN: token, CONTROLHUB_OBS_URL: "ws://127.0.0.1:1" }, stdio: ["ignore", "pipe", "pipe"] });
let log = "";
child.stdout.on("data", d => { log = (log + d).slice(-4000); });
child.stderr.on("data", d => { log = (log + d).slice(-4000); });
const base = "http://127.0.0.1:5187/api/v1/";
const headers = { Authorization:"Bearer " + token, "Content-Type":"application/json" };
const post = body => fetch(base + "commands", {method:"POST",headers,body:JSON.stringify(body)});
(async () => {
  try {
    let ready = false;
    for(let i=0;i<100;i++) {
      try { if((await fetch(base+"plugins")).status===401) {ready=true;break;} } catch {}
      if(child.exitCode !== null) throw new Error("Host exited: " + log);
      await new Promise(resolve=>setTimeout(resolve,100));
    }
    assert.ok(ready, "Host did not start: " + log);
    assert.equal((await fetch(base+"plugins",{headers:{Authorization:"Bearer wrong"}})).status,401);
    assert.equal((await fetch(base+"plugins",{headers:{...headers,Origin:"https://example.com"}})).status,403);
    const catalog = await (await fetch(base+"plugins",{headers})).json();
    assert.ok(catalog.some(p=>p.id==="powerpoint"));
    assert.ok(catalog.some(p=>p.id==="obs"));
    assert.equal((await fetch(base+"commands",{method:"POST",headers,body:"{"})).status,400);
    assert.equal((await fetch(base+"commands",{method:"POST",headers,body:"null"})).status,400);
    const command={protocolVersion:"1",id:randomUUID(),plugin:"powerpoint",action:"save",arguments:{}};
    assert.equal((await (await post(command)).json()).code,"unsupported_action");
    command.protocolVersion="2";
    assert.equal((await (await post(command)).json()).code,"unsupported_protocol");
    command.protocolVersion="1"; command.action=null;
    assert.equal((await (await post(command)).json()).code,"invalid_command");
    const status=await (await fetch(base+"status",{headers})).json();
    assert.equal(status.version,"0.0.001");
    assert.equal(status.plugins.find(p=>p.id==="androidremote").state,"not_implemented");
    assert.equal(status.plugins.find(p=>p.id==="obs").state,"unavailable");
    const { createBridge } = require('../Plugins/StreamDeck/dk.thebonden.controlhub.sdPlugin/bridge.js');
    const messages = [];
    const bridge = createBridge(message => messages.push(message));
    const action = 'dk.thebonden.controlhub.next';
    await bridge.handle({event:'willAppear',context:'smoke-key',action,payload:{settings:{token}}});
    await bridge.handle({event:'sendToPlugin',context:'smoke-key',action,
      payload:{kind:'checkConnection',requestId:'smoke-check',token}});
    assert.equal(messages.length,1);
    assert.equal(messages[0].event,'sendToPropertyInspector');
    assert.equal(messages[0].payload.requestId,'smoke-check');
    assert.equal(messages[0].payload.code,'partial');
    assert.equal(messages[0].payload.checks.find(p=>p.id==='obs').state,'unavailable');
    assert.ok(!JSON.stringify(messages).includes(token));
    console.log("Host and Stream Deck connection checks passed / Host- og Stream Deck-forbindelseskontrol bestået");
  } finally { child.kill(); }
})().catch(error=>{ console.error(error); process.exitCode=1; });
