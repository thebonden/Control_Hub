"use strict";
const { definition, makeCommand, statusTitle } = require("./actions.js");
function createBridge(send, fetchImpl = fetch) {
  const contexts = new Map();
  const busy = new Set();
  function title(context, text) { send({ event: "setTitle", context, payload: { title: text, target: 0 } }); }
  async function invoke(context) {
    const item = contexts.get(context);
    if (!item || busy.has(context)) return;
    busy.add(context);
    try {
      const settings = item.settings;
      if (typeof settings.token !== "string" || settings.token.length < 32) throw new Error("Token missing");
      const isStatus = item.action === "dk.thebonden.controlhub.status";
      const command = isStatus ? null : makeCommand(item.action, settings);
      const response = await fetchImpl("http://127.0.0.1:5187/api/v1/" + (isStatus ? "status" : "commands"), {
        method: isStatus ? "GET" : "POST",
        headers: { Authorization: "Bearer " + settings.token, "Content-Type": "application/json" },
        ...(command ? { body: JSON.stringify(command) } : {}),
        signal: AbortSignal.timeout(isStatus ? 35000 : 12000),
        redirect: "error"
      });
      if (!response.ok) throw new Error("HTTP error");
      const result = await response.json();
      if (!contexts.has(context)) return;
      if (isStatus) {
        title(context, statusTitle(result, settings.language));
      } else {
        if (result.protocolVersion !== "1" || result.id !== command.id || result.success !== true) throw new Error("Command failed");
        title(context, definition(item.action)[settings.language === "en" ? "en" : "da"]);
        send({ event: "showOk", context });
      }
    } catch {
      if (contexts.has(context)) {
        title(context, item.settings.language === "en" ? "Check\nstatus" : "Tjek\nstatus");
        send({ event: "showAlert", context });
      }
    } finally { busy.delete(context); }
  }
  async function handle(message) {
    if (message.event === "willDisappear") { contexts.delete(message.context); return; }
    if (message.event === "willAppear" || message.event === "didReceiveSettings") {
      definition(message.action);
      contexts.set(message.context, { action: message.action, settings: message.payload?.settings ?? {} });
      if (message.action === "dk.thebonden.controlhub.status") await invoke(message.context);
    }
    if (message.event === "keyDown") await invoke(message.context);
  }
  async function poll() {
    await Promise.all([...contexts].filter(([,v]) => v.action === "dk.thebonden.controlhub.status").map(([id]) => invoke(id)));
  }
  return { handle, poll };
}
module.exports = { createBridge };
