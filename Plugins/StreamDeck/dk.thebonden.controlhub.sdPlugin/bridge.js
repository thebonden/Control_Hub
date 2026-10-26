"use strict";
const { definition, makeCommand, statusTitle } = require("./actions.js");
const { connectionReport, failure } = require("./diagnostics.js");
function createBridge(send, fetchImpl = fetch) {
  const contexts = new Map();
  const busy = new Set();
  const checking = new Set();
  function title(context, text) { send({ event: "setTitle", context, payload: { title: text, target: 0 } }); }
  function current(context, item) { return contexts.get(context) === item; }
  function validToken(token) { return typeof token === "string" && token.length >= 32 && token.length <= 4096; }
  async function checkConnection(message) {
    const item = contexts.get(message.context);
    if (!item || item.action !== message.action || checking.has(message.context) ||
        message.payload?.kind !== "checkConnection" || typeof message.payload.requestId !== "string" ||
        message.payload.requestId.length > 64) return;
    checking.add(message.context);
    let report;
    try {
      const token = message.payload.token;
      if (!validToken(token)) report = failure("invalid_token");
      else {
        const response = await fetchImpl("http://127.0.0.1:5187/api/v1/status", {
          method: "GET", headers: { Authorization: "Bearer " + token },
          signal: AbortSignal.timeout(35000), redirect: "error"
        });
        report = !response.ok ? failure(response.status === 401 ? "unauthorized" : response.status === 429 ? "busy" : "unreachable") :
          connectionReport(await response.json());
      }
    } catch { report = failure("unreachable"); }
    finally { checking.delete(message.context); }
    if (current(message.context, item)) send({ event: "sendToPropertyInspector", context: message.context,
      payload: { kind: "connectionResult", requestId: message.payload.requestId, ...report } });
  }
  async function invoke(context) {
    const item = contexts.get(context);
    if (!item || busy.has(context)) return;
    busy.add(context);
    try {
      const settings = item.settings;
      if (!validToken(settings.token)) throw new Error("Token missing / Token mangler");
      const isStatus = item.action === "dk.thebonden.controlhub.status";
      const command = isStatus ? null : makeCommand(item.action, settings);
      const response = await fetchImpl("http://127.0.0.1:5187/api/v1/" + (isStatus ? "status" : "commands"), {
        method: isStatus ? "GET" : "POST",
        headers: { Authorization: "Bearer " + settings.token, "Content-Type": "application/json" },
        ...(command ? { body: JSON.stringify(command) } : {}),
        signal: AbortSignal.timeout(isStatus ? 35000 : 12000),
        redirect: "error"
      });
      if (!response.ok) throw new Error("HTTP error / HTTP-fejl");
      const result = await response.json();
      if (!current(context, item)) return;
      if (isStatus) {
        title(context, statusTitle(result, settings.language));
      } else {
        if (result.protocolVersion !== "1" || result.id !== command.id || result.success !== true) throw new Error("Command failed / Kommando fejlede");
        title(context, definition(item.action)[settings.language === "en" ? "en" : "da"]);
        send({ event: "showOk", context });
      }
    } catch {
      if (current(context, item)) {
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
    if (message.event === "sendToPlugin") await checkConnection(message);
    if (message.event === "keyDown") await invoke(message.context);
  }
  async function poll() {
    await Promise.all([...contexts].filter(([,v]) => v.action === "dk.thebonden.controlhub.status").map(([id]) => invoke(id)));
  }
  return { handle, poll };
}
module.exports = { createBridge };
