// Records Thinking Orbs frames from the original orb-core.ts so the C# port can be checked against it.
// Usage: node --experimental-strip-types gen.mjs <path-to-orb-core.ts> <out.json>
import { pathToFileURL } from "node:url";
import { writeFileSync } from "node:fs";

const [corePath, outPath] = process.argv.slice(2);

let now = 1000;
let rafCallback = null;
Object.defineProperty(globalThis, "performance", { value: { now: () => now }, configurable: true });
globalThis.requestAnimationFrame = (cb) => { rafCallback = cb; return 1; };
globalThis.cancelAnimationFrame = () => { rafCallback = null; };
globalThis.matchMedia = () => ({ matches: false });
globalThis.IntersectionObserver = class { observe() {} disconnect() {} };

class FakeElement {
  constructor() { this.attrs = {}; }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  removeAttribute(k) { delete this.attrs[k]; }
}
globalThis.document = { createElementNS: () => new FakeElement() };

const { mountOrb, VARIANTS } = await import(pathToFileURL(corePath).href);

const STEP = 50;                        // ms per frame, the cadence the C# test replays
const SAMPLES = [0, 400, 1250, 3000, 6100];
const looks = Object.entries(VARIANTS).flatMap(([state, variants]) => variants.map((variant) => ({ state, variant })));
const cases = [
  ...looks.map((look) => ({ ...look, size: 20 })),
  { state: "searching", variant: "default", size: 64 },
  { state: "working", variant: "default", size: 64 },
  { state: "reasoning", variant: "default", size: 64 },
];

const out = { step: STEP, samples: SAMPLES, cases: [] };
for (const c of cases) {
  // A fresh speed per case gives each its own clock, starting at t = 0.
  const speed = 1 + out.cases.length * 1e-9;
  const els = [];
  const svg = {
    setAttribute() {}, removeAttribute() {}, replaceChildren() {},
    appendChild(el) { els.push(el); return el; },
  };
  mountOrb(svg, { state: c.state, variant: c.variant, size: c.size, speed });
  const frames = {};
  const capture = (t) => {
    frames[t] = els.map((e) => [+e.attrs.cx, +e.attrs.cy, +e.attrs.r, +e.attrs["fill-opacity"]]);
  };
  capture(0);
  let t = 0;
  const last = SAMPLES[SAMPLES.length - 1];
  while (t < last) {
    now += STEP / speed;                 // the clock advances by elapsed * speed
    t += STEP;
    rafCallback(now);
    if (SAMPLES.includes(t)) capture(t);
  }
  out.cases.push({ state: c.state, variant: c.variant, size: c.size, dots: els.length, frames });
}
writeFileSync(outPath, JSON.stringify(out));
console.log(`wrote ${out.cases.length} cases`);
