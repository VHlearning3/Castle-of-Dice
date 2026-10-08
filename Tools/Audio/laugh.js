// Synthesises the Mirror Twin's laugh (Castle of Dice, 2026-10-08): a run of falling "ha"s through female /a/
// formants, with breath on each onset and a cold, mirror-like echo. Writes 22050 Hz mono 16-bit WAV.
// Run: node Tools/Audio/laugh.js Assets/Audio/MirrorTwin_Laugh.wav
const fs = require("fs");
const SR = 22050;
const out = process.argv[2] || "MirrorTwin_Laugh.wav";
let seed = 12345;
const rnd = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);

const total = Math.floor(SR * 3.4);
const dry = new Float32Array(total);

// Syllables: [start s, length s, pitch Hz, loudness]
const syl = [];
let t = 0.12;
const n = 10;
for (let i = 0; i < n; i++) {
  const p = 430 - i * 13 + (i === 0 ? 40 : 0);
  const len = i === n - 1 ? 0.42 : 0.13 + 0.01 * Math.sin(i);
  syl.push([t, len, p, i === n - 1 ? 0.8 : 1 - i * 0.04]);
  t += len + (i === n - 1 ? 0 : 0.075 + 0.01 * (i % 3));
}

function biquadBP(f, bw) {
  const w = 2 * Math.PI * f / SR, q = f / bw, a = Math.sin(w) / (2 * q);
  const b0 = a, b2 = -a, a0 = 1 + a, a1 = -2 * Math.cos(w), a2 = 1 - a;
  let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
  return (x) => {
    const y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0;
    x2 = x1; x1 = x; y2 = y1; y1 = y;
    return y;
  };
}
const formants = [[950, 110, 1.0], [1320, 140, 0.55], [2900, 220, 0.28], [3700, 300, 0.14]].map(([f, b, g]) => [biquadBP(f, b), g]);
const breath = biquadBP(1800, 1600);

let phase = 0;
for (let i = 0; i < total; i++) {
  const time = i / SR;
  let src = 0, noise = 0, env = 0;
  for (const [s, len, p, loud] of syl) {
    if (time < s - 0.05 || time > s + len + 0.02) continue;
    const u = (time - s) / len;
    // breathy "h" just before the vowel, then the voiced "a" swelling and falling away
    if (time < s) noise += loud * 0.5 * (1 - (s - time) / 0.05);
    else {
      const e = Math.min(1, u / 0.12) * Math.pow(Math.max(0, 1 - u), 1.3);
      env = Math.max(env, e * loud);
      noise += 0.18 * e * loud;
      const f0 = p * (1 - 0.18 * u) * (1 + 0.012 * Math.sin(2 * Math.PI * 5.5 * time)) * (1 + 0.004 * (rnd() - 0.5));
      phase += f0 / SR;
      phase -= Math.floor(phase);
    }
  }
  // glottal pulse: a skewed bump each period
  const g = phase < 0.6 ? Math.sin(Math.PI * phase / 0.6) ** 2 : 0;
  src = (g - 0.3) * env;
  let v = 0;
  for (const [bp, gain] of formants) v += bp(src) * gain;
  v += breath((rnd() * 2 - 1) * noise) * 0.6;
  dry[i] = v;
}

// Cold echo: four combs into two all-passes, mixed in behind the voice
function comb(d, fb) { const buf = new Float32Array(d); let k = 0; return (x) => { const y = buf[k]; buf[k] = x + y * fb; k = (k + 1) % d; return y; }; }
function allpass(d, g) { const buf = new Float32Array(d); let k = 0; return (x) => { const b = buf[k]; const y = -x + b; buf[k] = x + b * g; k = (k + 1) % d; return y; }; }
const combs = [1116, 1188, 1277, 1356].map((d) => comb(Math.floor(d * SR / 44100), 0.78));
const aps = [556, 441].map((d) => allpass(Math.floor(d * SR / 44100), 0.5));
const wet = new Float32Array(total);
for (let i = 0; i < total; i++) {
  let s = 0;
  for (const c of combs) s += c(dry[i]);
  for (const a of aps) s = a(s);
  wet[i] = s * 0.25;
}
let peak = 0;
const mix = new Float32Array(total);
for (let i = 0; i < total; i++) { mix[i] = dry[i] + wet[i]; peak = Math.max(peak, Math.abs(mix[i])); }
const gain = 0.85 / (peak || 1);
const fadeOut = Math.floor(SR * 0.3);
const pcm = Buffer.alloc(total * 2);
for (let i = 0; i < total; i++) {
  let s = mix[i] * gain;
  if (i > total - fadeOut) s *= (total - i) / fadeOut;
  pcm.writeInt16LE(Math.max(-32767, Math.min(32767, Math.round(s * 32767))), i * 2);
}
const header = Buffer.alloc(44);
header.write("RIFF", 0); header.writeUInt32LE(36 + pcm.length, 4); header.write("WAVE", 8);
header.write("fmt ", 12); header.writeUInt32LE(16, 16); header.writeUInt16LE(1, 20); header.writeUInt16LE(1, 22);
header.writeUInt32LE(SR, 24); header.writeUInt32LE(SR * 2, 28); header.writeUInt16LE(2, 32); header.writeUInt16LE(16, 34);
header.write("data", 36); header.writeUInt32LE(pcm.length, 40);
fs.writeFileSync(out, Buffer.concat([header, pcm]));
console.log("wrote", out, (total / SR).toFixed(2) + " s");
