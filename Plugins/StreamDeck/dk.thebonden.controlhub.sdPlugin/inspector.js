"use strict";
let socket, context, currentSettings = {}, action;
const el = id => document.getElementById(id);
const translations = {
  da: { tokenLabel: "Control Hub-adgangstoken", slideLabel: "Slidenummer (fra 1)", sceneLabel: "Præcist scenenavn i OBS", inputLabel: "Præcist navn på lydkilde i OBS", muteLabel: "Ønsket lydtilstand", hint: "Kører kun lokalt. Token gemmes i Stream Deck-indstillinger. Del eller eksportér ikke profiler med token. En fejl kan betyde ukendt udfald; kontrollér status før nyt tryk.", save: "Gem", saved: "Gemt" },
  en: { tokenLabel: "Control Hub access token", slideLabel: "Slide number (starting at 1)", sceneLabel: "Exact OBS scene name", inputLabel: "Exact OBS input name", muteLabel: "Desired mute state", hint: "Local use only. Token is stored in Stream Deck settings. Do not share or export profiles containing it. An error can mean an unknown outcome; check status before pressing again.", save: "Save", saved: "Saved" }
};
function render() {
  const language = el("language").value;
  document.documentElement.lang = language;
  for (const [id, value] of Object.entries(translations[language])) if (id !== "saved") el(id).textContent = value;
}
function load(settings) {
  currentSettings = settings;
  for (const key of ["token", "slide", "sceneName", "inputName"]) el(key).value = settings[key] ?? "";
  el("language").value = settings.language === "en" ? "en" : "da";
  el("muted").value = settings.muted === false ? "false" : "true";
  render();
}
function connectElgatoStreamDeckSocket(port, uuid, registerEvent, info, actionInfo) {
  const data = JSON.parse(actionInfo);
  context = uuid; action = data.action;
  load(data.payload?.settings ?? {});
  el("slideField").hidden = !action.endsWith(".goto");
  el("sceneField").hidden = !action.endsWith(".scene");
  el("inputField").hidden = !action.endsWith(".mute");
  socket = new WebSocket("ws://127.0.0.1:" + port);
  socket.onopen = () => socket.send(JSON.stringify({ event: registerEvent, uuid }));
  socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.event === "didReceiveSettings") load(message.payload.settings);
  };
}
el("language").onchange = render;
el("save").onclick = () => {
  if (!socket || socket.readyState !== WebSocket.OPEN) return;
  const settings = { ...currentSettings, token: el("token").value, language: el("language").value,
    slide: el("slide").value, sceneName: el("sceneName").value, inputName: el("inputName").value, muted: el("muted").value === "true" };
  socket.send(JSON.stringify({ event: "setSettings", context, payload: settings }));
  el("saved").textContent = translations[settings.language].saved;
};
render();
