const { test } = require("node:test");
const assert = require("node:assert/strict");
const vm = require("node:vm");
const fs = require("node:fs");
const path = require("node:path");
const source = fs.readFileSync(path.join(__dirname,"../Plugins/StreamDeck/dk.thebonden.controlhub.sdPlugin/inspector.js"),"utf8");
function inspector(language="da") {
  const elements=new Map(),sent=[];
  const el=id=>{if(!elements.has(id))elements.set(id,{value:"",textContent:"",disabled:false});return elements.get(id);};
  let socket,timer;
  class Socket {
    static OPEN=1;
    readyState=0;
    constructor(){socket=this;}
    send(message){sent.push(JSON.parse(message));}
  }
  const context=vm.createContext({document:{getElementById:el,documentElement:{}},WebSocket:Socket,
    crypto:{randomUUID:()=>"request-1"},setTimeout:fn=>{timer=fn;return 1;},clearTimeout:()=>{timer=null;}});
  vm.runInContext(source,context);
  context.connectElgatoStreamDeckSocket(1234,"inspector-session","registerPropertyInspector","{}",JSON.stringify({
    context:"key",action:"dk.thebonden.controlhub.next",payload:{settings:{language,token:"t".repeat(40)}}}));
  socket.readyState=1;socket.onopen();
  return {el,sent,socket,timeout:()=>timer?.()};
}
test("inspector connection check sends no command and does not save credentials",()=>{
  const ui=inspector();
  ui.el("token").value="new".repeat(20);
  ui.el("check").onclick();
  assert.deepEqual(ui.sent.map(m=>m.event),["registerPropertyInspector","sendToPlugin"]);
  assert.equal(ui.sent[1].payload.kind,"checkConnection");
  assert.equal(ui.sent[0].uuid,"inspector-session");
  assert.equal(ui.sent[1].context,"inspector-session");
  assert.equal(ui.sent[1].payload.token,"new".repeat(20));
  assert.equal(ui.el("check").disabled,true);
});
test("distinct inspector registration and action contexts complete a real bridge round trip",async()=>{
  const { createBridge }=require("../Plugins/StreamDeck/dk.thebonden.controlhub.sdPlugin/bridge.js");
  const ui=inspector();
  const bridge=createBridge(message=>ui.socket.onmessage({data:JSON.stringify(message)}),async()=>({ok:true,json:async()=>({
    protocolVersion:"1",version:"0.0.001",plugins:[
      {id:"powerpoint",state:"unavailable"},{id:"obs",state:"unavailable"}
    ]
  })}));
  await bridge.handle({event:"willAppear",context:"key",action:"dk.thebonden.controlhub.next",payload:{settings:{}}});
  ui.el("check").onclick();
  const sent=ui.sent.at(-1);
  assert.equal(sent.context,"inspector-session");
  // Stream Deck validates the UI sender and forwards the action's key context.
  await bridge.handle({...sent,context:"key"});
  assert.equal(ui.el("check").disabled,false);
  assert.match(ui.el("connection").textContent,/0\.0\.001/);
  ui.el("save").onclick();
  assert.equal(ui.sent.at(-1).event,"setSettings");
  assert.equal(ui.sent.at(-1).action,"dk.thebonden.controlhub.next");
  assert.equal(ui.sent.at(-1).context,"inspector-session");
});
test("inspector ignores mismatched results and shows matching bilingual result as text",()=>{
  const ui=inspector("en");ui.el("check").onclick();
  const payload={kind:"connectionResult",requestId:"wrong",message:{da:"Forbundet",en:"Connected"},checks:[]};
  ui.socket.onmessage({data:JSON.stringify({event:"sendToPropertyInspector",context:"key",payload})});
  assert.equal(ui.el("check").disabled,true);
  payload.requestId="request-1";
  payload.message.en="<script>plain text</script>";
  ui.socket.onmessage({data:JSON.stringify({event:"sendToPropertyInspector",context:"key",payload})});
  assert.equal(ui.el("connection").textContent,"<script>plain text</script>");
  assert.equal(ui.el("check").disabled,false);
  ui.el("language").value="da";ui.el("language").onchange();
  assert.equal(ui.el("connection").textContent,"Forbundet");
});
test("editing token invalidates an outstanding diagnostic result",()=>{
  const ui=inspector();ui.el("check").onclick();ui.el("token").oninput();
  ui.socket.onmessage({data:JSON.stringify({event:"sendToPropertyInspector",context:"key",
    payload:{kind:"connectionResult",requestId:"request-1",message:{da:"Old",en:"Old"},checks:[]}})});
  assert.equal(ui.el("connection").textContent,"");
});
test("inspector timeout unlocks controls without sending another request",()=>{
  const ui=inspector("en");ui.el("check").onclick();ui.timeout();
  assert.equal(ui.el("check").disabled,false);
  assert.match(ui.el("connection").textContent,/did not respond/);
  assert.equal(ui.sent.length,2);
});
