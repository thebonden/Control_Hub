"use strict";
const failures = {
  invalid_token: ["Indsæt et token på mindst 32 tegn.", "Enter a token of at least 32 characters."],
  unauthorized: ["Token blev afvist af Control Hub.", "Control Hub rejected the token."],
  busy: ["Control Hub er optaget. Prøv forbindelsestesten igen senere.", "Control Hub is busy. Try the connection check again later."],
  unreachable: ["Control Hub kunne ikke kontaktes. Kontrollér, at værten kører.", "Could not reach Control Hub. Check that the host is running."],
  invalid_response: ["Control Hub returnerede en ukendt statusprotokol.", "Control Hub returned an unrecognized status protocol."]
};
function failure(code) {
  const [da, en] = failures[code] ?? failures.unreachable;
  return { code, message: { da, en }, checks: [] };
}
function connectionReport(status) {
  if (status?.protocolVersion !== "1" || typeof status.version !== "string" ||
      !/^\d+\.\d+\.\d+$/.test(status.version) || !Array.isArray(status.plugins))
    return failure("invalid_response");
  const checks = [];
  for (const id of ["powerpoint", "obs"]) {
    const matches = status.plugins.filter(p => p?.id === id);
    if (matches.length !== 1 || !["ready", "unavailable", "unsupported", "not_implemented"].includes(matches[0].state))
      return failure("invalid_response");
    const state = matches[0].state;
    const ready = state === "ready";
    const message = id === "powerpoint" ?
      { da: ready ? "PowerPoint: slideshow tilsluttet." : "PowerPoint: start ét slideshow, eller kontrollér forbindelsen.",
        en: ready ? "PowerPoint: slideshow connected." : "PowerPoint: start one slideshow or check the connection." } :
      { da: ready ? "OBS: tilsluttet." : "OBS: kontrollér WebSocket-server, port og adgangskode.",
        en: ready ? "OBS: connected." : "OBS: check the WebSocket server, port and password." };
    checks.push({ id, state, message });
  }
  const ready = checks.every(c => c.state === "ready");
  return { code: ready ? "ready" : "partial", version: status.version,
    message: ready ? { da: "Control Hub, PowerPoint og OBS svarer.", en: "Control Hub, PowerPoint and OBS are responding." } :
      { da: "Control Hub svarer; en eller flere integrationer er utilgængelige.",
        en: "Control Hub is responding; one or more integrations are unavailable." }, checks };
}
module.exports = { connectionReport, failure };
