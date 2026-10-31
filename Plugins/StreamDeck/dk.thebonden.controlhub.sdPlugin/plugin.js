"use strict";
const { createBridge } = require("./bridge.js");
const args = process.argv.slice(2);
function argument(name) {
  const index = args.indexOf(name);
  if (index < 0 || !args[index + 1]) throw new Error("Missing Stream Deck argument / Stream Deck-argument mangler");
  return args[index + 1];
}
const port = Number(argument("-port"));
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error("Invalid port / Ugyldig port");
const uuid = argument("-pluginUUID");
const registerEvent = argument("-registerEvent");
const socket = new WebSocket("ws://127.0.0.1:" + port);
const send = message => { if (socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify(message)); };
const bridge = createBridge(send);
socket.addEventListener("open", () => send({ event: registerEvent, uuid }));
socket.addEventListener("message", event => {
  try { void bridge.handle(JSON.parse(event.data)).catch(() => {}); } catch {}
});
const interval = setInterval(() => { void bridge.poll(); }, 3000);
socket.addEventListener("close", () => { clearInterval(interval); process.exit(0); });
socket.addEventListener("error", () => { clearInterval(interval); process.exit(1); });
