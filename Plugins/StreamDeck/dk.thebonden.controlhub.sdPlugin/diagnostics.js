"use strict";
// Faste, lokale tekster ud fra kendte årsager; vis aldrig rå serverfejl.
// Fixed local text for known reasons; never display raw server errors.
const issues = {
  powerpoint: {
    powerpoint_not_found: ["PowerPoint kunne ikke findes. Åbn programmet og start ét slideshow.", "PowerPoint could not be found. Open it and start one slideshow."],
    no_slideshow: ["PowerPoint er åbent, men intet slideshow kører. Start visningen.", "PowerPoint is open, but no slideshow is running. Start the show."],
    multiple_slideshows: ["Flere slideshows kører. Lad kun ét slideshow være aktivt.", "Multiple slideshows are running. Leave exactly one show active."],
    powerpoint_unavailable: ["PowerPoint svarer ikke. Luk eventuelle dialoger og prøv status igen.", "PowerPoint is unavailable. Close any dialogs and check status again."]
  },
  obs: {
    connection_unavailable: ["OBS kan ikke kontaktes. Start OBS, aktivér WebSocket-serveren og kontrollér porten.", "OBS cannot be reached. Start OBS, enable its WebSocket server and check the port."],
    password_missing: ["OBS kræver en adgangskode. Stop Control Hub, indtast den og start igen.", "OBS requires a password. Stop Control Hub, enter it and start again."],
    authentication_failed: ["OBS afviste adgangskoden. Stop Control Hub, ret adgangskoden og start igen.", "OBS rejected the password. Stop Control Hub, correct the password and start again."],
    protocol_error: ["Svaret passer ikke til OBS WebSocket v5. Kontrollér server og port.", "The reply is not compatible with OBS WebSocket v5. Check the server and port."],
    connection_lost: ["OBS-forbindelsen blev afbrudt. Kontrollér status før en ny handling.", "The OBS connection was interrupted. Check status before another action."],
    request_rejected: ["OBS afviste forespørgslen. Kontrollér scenenavn, lydkilde og status.", "OBS rejected the request. Check the scene name, audio input and status."]
  }
};
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
    let message = id === "powerpoint" ?
      { da: ready ? "PowerPoint: slideshow tilsluttet." : "PowerPoint: start ét slideshow, eller kontrollér forbindelsen.",
        en: ready ? "PowerPoint: slideshow connected." : "PowerPoint: start one slideshow or check the connection." } :
      { da: ready ? "OBS: tilsluttet." : "OBS: kontrollér WebSocket-server, port og adgangskode.",
        en: ready ? "OBS: connected." : "OBS: check the WebSocket server, port and password." };
    const reason = matches[0].data?.reason;
    if (!ready && typeof reason === "string" && Object.hasOwn(issues[id], reason)) {
      const [da, en] = issues[id][reason];
      message = { da, en };
    }
    checks.push({ id, state, message });
  }
  const ready = checks.every(c => c.state === "ready");
  return { code: ready ? "ready" : "partial", version: status.version,
    message: ready ? { da: "Control Hub, PowerPoint og OBS svarer.", en: "Control Hub, PowerPoint and OBS are responding." } :
      { da: "Control Hub svarer; en eller flere integrationer er utilgængelige.",
        en: "Control Hub is responding; one or more integrations are unavailable." }, checks };
}
module.exports = { connectionReport, failure };
