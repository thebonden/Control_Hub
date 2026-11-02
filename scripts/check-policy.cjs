const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");
const root = path.resolve(__dirname,"..");
function walk(dir) {
  return fs.readdirSync(dir,{withFileTypes:true}).flatMap(item => {
    if([".git","bin","obj","node_modules","artifacts"].includes(item.name)) return [];
    const file=path.join(dir,item.name);
    return item.isDirectory()?walk(file):[file];
  });
}
const files=walk(root);
assert.ok(!files.some(file=>/\.(ppt|pptx|pptm|pps|ppsx|ppsm|pot|potx|potm)$/i.test(file)), "Presentation files are not source assets / Præsentationsfiler må ikke tilføjes");
const source=files.filter(file=>file.includes(path.join("Plugins","PowerPoint"))&&file.endsWith(".cs")).map(file=>fs.readFileSync(file,"utf8")).join("\n");
assert.ok(!/SendKeys|SendInput|keybd_event|SetForegroundWindow|\.Save\s*\(|\.SaveAs\s*\(|\.Open\s*\(|\.Quit\s*\(/.test(source), "Forbidden PowerPoint file/focus operation / Forbudt fil- eller fokusoperation");
const version=fs.readFileSync(path.join(root,"VERSION"),"utf8").trim();
assert.equal(version,"0.0.001", "Version changes require explicit review / Versionsændringer kræver eksplicit review");
console.log("Source policy checks passed / Kildekoderegler bestået");
