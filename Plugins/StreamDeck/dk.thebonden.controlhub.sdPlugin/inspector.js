"use strict";
let socket, context, actionContext, currentSettings = {}, action, pendingCheck = null, checkTimer, lastReport = null;
const el = id => document.getElementById(id);
const translations = {
  da: { tokenLabel: "Control Hub-adgangstoken", slideLabel: "Slidenummer (fra 1)", sceneLabel: "Præcist scenenavn i OBS", inputLabel: "Præcist navn på lydkilde i OBS", muteLabel: "Ønsket lydtilstand",
    hint: "Kører kun lokalt. Token gemmes i Stream Deck-indstillinger. Del ikke profiler med token. Forbindelsestesten læser kun status og bruger tokenet i feltet uden at gemme det.",
    save: "Gem", check: "Test forbindelse", saved: "Gemt", checking: "Kontrollerer Control Hub, PowerPoint og OBS…",
    timeout: "Forbindelsestesten svarede ikke. Kontrollér, at Control Hub kører.", disconnected: "Forbindelsen til Stream Deck er afbrudt." },
  en: { tokenLabel: "Control Hub access token", slideLabel: "Slide number (starting at 1)", sceneLabel: "Exact OBS scene name", inputLabel: "Exact OBS input name", muteLabel: "Desired mute state",
    hint: "Local use only. Token is stored in Stream Deck settings. Do not share profiles containing it. The connection check only reads status and uses the entered token without saving it.",
    save: "Save", check: "Test connection", saved: "Saved", checking: "Checking Control Hub, PowerPoint and OBS…",
    timeout: "The connection check did not respond. Check that Control Hub is running.", disconnected: "Disconnected from Stream Deck." }
};
function language() { return el("language").value === "en" ? "en" : "da"; }
function showReport() {
  if (!lastReport) return;
  const lang = language();
  const lines = [lastReport.message?.[lang] ?? translations[lang].timeout];
  if (lastReport.version) lines.push("Control Hub " + lastReport.version);
  for (const check of lastReport.checks ?? []) if (check.message?.[lang]) lines.push(check.message[lang]);
  el("connection").textContent = lines.join("\n");
}
function render() {
  const lang = language();
  document.documentElement.lang = lang;
  for (const id of ["tokenLabel","slideLabel","sceneLabel","inputLabel","muteLabel","hint","save","check"])
    el(id).textContent = translations[lang][id];
  if (pendingCheck) el("connection").textContent = translations[lang].checking;
  else showReport();
}
function clearCheck() {
  clearTimeout(checkTimer);
  pendingCheck = null;
  const disconnected = !socket || socket.readyState !== WebSocket.OPEN;
  el("check").disabled = disconnected;
  el("save").disabled = disconnected;
}
function invalidateCheck() {
  clearCheck(); lastReport = null; el("connection").textContent = ""; el("saved").textContent = "";
}
function load(settings) {
  currentSettings = settings;
  invalidateCheck();
  for (const key of ["token", "slide", "sceneName", "inputName"]) el(key).value = settings[key] ?? "";
  el("language").value = settings.language === "en" ? "en" : "da";
  el("muted").value = settings.muted === false ? "false" : "true";
  render();
}
function connectElgatoStreamDeckSocket(port, uuid, registerEvent, info, actionInfo) {
  const data = JSON.parse(actionInfo);
  // Send with the UI registration UUID; replies identify the key instance.
  // Send med UI-registrerings-id; svar identificerer den konkrete knap.
  context = uuid; actionContext = data.context; action = data.action;
  if (typeof actionContext !== "string" || !actionContext) throw new Error("Missing action context / Knapkontekst mangler");
  load(data.payload?.settings ?? {});
  el("slideField").hidden = !action.endsWith(".goto");
  el("sceneField").hidden = !action.endsWith(".scene");
  el("inputField").hidden = !action.endsWith(".mute");
  socket = new WebSocket("ws://127.0.0.1:" + port);
  socket.onopen = () => { socket.send(JSON.stringify({ event: registerEvent, uuid })); clearCheck(); };
  socket.onclose = () => { clearCheck(); el("connection").textContent = translations[language()].disconnected; };
  socket.onmessage = event => {
    let message;
    try { message = JSON.parse(event.data); } catch { return; }
    const forThisInspector = message.context === actionContext || message.context === context;
    if (message.event === "didReceiveSettings" && forThisInspector) load(message.payload.settings);
    if (message.event === "sendToPropertyInspector" && forThisInspector &&
        message.payload?.kind === "connectionResult" && pendingCheck && message.payload.requestId === pendingCheck) {
      clearCheck(); lastReport = message.payload; showReport();
    }
  };
}
el("language").onchange = render;
el("token").oninput = invalidateCheck;
el("save").onclick = () => {
  if (!socket || socket.readyState !== WebSocket.OPEN || pendingCheck) return;
  const settings = { ...currentSettings, token: el("token").value, language: language(),
    slide: el("slide").value, sceneName: el("sceneName").value, inputName: el("inputName").value, muted: el("muted").value === "true" };
  socket.send(JSON.stringify({ event: "setSettings", action, context, payload: settings }));
  el("saved").textContent = translations[settings.language].saved;
};
el("check").onclick = () => {
  if (!socket || socket.readyState !== WebSocket.OPEN || pendingCheck) return;
  pendingCheck = crypto.randomUUID(); lastReport = null;
  el("check").disabled = true; el("save").disabled = true;
  el("connection").textContent = translations[language()].checking;
  socket.send(JSON.stringify({ event: "sendToPlugin", action, context,
    payload: { kind: "checkConnection", requestId: pendingCheck, token: el("token").value } }));
  checkTimer = setTimeout(() => { clearCheck(); el("connection").textContent = translations[language()].timeout; }, 40000);
};
render();
