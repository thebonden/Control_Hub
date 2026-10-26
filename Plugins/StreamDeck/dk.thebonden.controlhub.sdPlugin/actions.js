"use strict";
const { randomUUID } = require("node:crypto");
const definitions = {
  "next": {
    "plugin": "powerpoint",
    "action": "next",
    "da": "Næste slide",
    "en": "Next slide"
  },
  "previous": {
    "plugin": "powerpoint",
    "action": "previous",
    "da": "Forrige slide",
    "en": "Previous slide"
  },
  "goto": {
    "plugin": "powerpoint",
    "action": "goto",
    "da": "Gå til slide",
    "en": "Go to slide"
  },
  "black": {
    "plugin": "powerpoint",
    "action": "black",
    "da": "Sort skærm",
    "en": "Black screen"
  },
  "resume": {
    "plugin": "powerpoint",
    "action": "resume",
    "da": "Vis slideshow",
    "en": "Resume slideshow"
  },
  "scene": {
    "plugin": "obs",
    "action": "scene.switch",
    "da": "Skift scene",
    "en": "Switch scene"
  },
  "recordstart": {
    "plugin": "obs",
    "action": "record.start",
    "da": "Start optagelse",
    "en": "Start recording"
  },
  "recordstop": {
    "plugin": "obs",
    "action": "record.stop",
    "da": "Stop optagelse",
    "en": "Stop recording"
  },
  "streamstart": {
    "plugin": "obs",
    "action": "stream.start",
    "da": "Start udsendelse",
    "en": "Start streaming"
  },
  "streamstop": {
    "plugin": "obs",
    "action": "stream.stop",
    "da": "Stop udsendelse",
    "en": "Stop streaming"
  },
  "mute": {
    "plugin": "obs",
    "action": "input.mute",
    "da": "Lyd fra/til",
    "en": "Set input mute"
  },
  "status": {
    "plugin": null,
    "action": null,
    "da": "Status",
    "en": "Status"
  }
};
function definition(uuid) {
  const prefix = "dk.thebonden.controlhub.";
  if (!uuid.startsWith(prefix)) throw new Error("Unknown action / Ukendt handling");
  const item = definitions[uuid.slice(prefix.length)];
  if (!item) throw new Error("Unknown action / Ukendt handling");
  return item;
}
function makeCommand(uuid, settings, id = randomUUID()) {
  const item = definition(uuid);
  if (!item.plugin) throw new Error("Status is read-only / Status er skrivebeskyttet");
  let args = {};
  if (item.action === "goto") {
    const slide = Number(settings.slide);
    if (!Number.isSafeInteger(slide) || slide < 1) throw new Error("Invalid slide / Ugyldig slide");
    args = { slide };
  }
  if (item.action === "scene.switch") {
    if (typeof settings.sceneName !== "string" || !settings.sceneName.trim()) throw new Error("Missing scene / Scene mangler");
    args = { sceneName: settings.sceneName };
  }
  if (item.action === "input.mute") {
    if (typeof settings.inputName !== "string" || !settings.inputName.trim() || typeof settings.muted !== "boolean")
      throw new Error("Invalid input / Ugyldig lydkilde");
    args = { inputName: settings.inputName, muted: settings.muted };
  }
  return { protocolVersion: "1", id, plugin: item.plugin, action: item.action, arguments: args };
}
function statusTitle(status, language = "da") {
  if (status.protocolVersion !== "1" || !Array.isArray(status.plugins)) throw new Error("Invalid status");
  const ppt = status.plugins.find(p => p.id === "powerpoint");
  const obs = status.plugins.find(p => p.id === "obs");
  const position = ppt?.state === "ready" ? ppt.data?.position ?? "?" : "?";
  const recording = obs?.state === "ready" ? (obs.data?.recording?.outputActive === true ? "ON" : obs.data?.recording?.outputActive === false ? "OFF" : "?") : "?";
  const streaming = obs?.state === "ready" ? (obs.data?.streaming?.outputActive === true ? "ON" : obs.data?.streaming?.outputActive === false ? "OFF" : "?") : "?";
  return "PPT " + position + "\n" + (language === "da" ? "OPT " : "REC ") + recording + "\nLIVE " + streaming;
}
module.exports = { definitions, definition, makeCommand, statusTitle };
