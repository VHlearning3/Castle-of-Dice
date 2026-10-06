// Castle of Dice: icons for the three worn magic items (Vili, 2026-10-06), in the faceted style of gen.js.
// Writes one standalone HTML page per icon (icon_<Name>.html); render each with headless Edge at 256x256:
//   msedge.exe --headless=new --default-background-color=00000000 --window-size=256,256 --screenshot=UI_Icon_<Name>.png icon_<Name>.html
const fs = require('fs');

// ---------- helpers (same as gen.js) ----------
function hexToRgb(h) { h = h.replace('#', ''); return [0, 2, 4].map(i => parseInt(h.substr(i, 2), 16)); }
function rgbToHex(r) { return '#' + r.map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join(''); }
function shade(h, t) {
  const c = hexToRgb(h);
  return rgbToHex(t >= 0 ? c.map(v => v + (255 - v) * t) : c.map(v => v * (1 + t)));
}
const LIGHT = norm([-0.55, -0.83]);
function norm(v) { const l = Math.hypot(v[0], v[1]) || 1; return [v[0] / l, v[1] / l]; }
function add(a, b) { return [a[0] + b[0], a[1] + b[1]]; }
function sub(a, b) { return [a[0] - b[0], a[1] - b[1]]; }
function mul(a, s) { return [a[0] * s, a[1] * s]; }
function lerp(a, b, t) { return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]; }
function centroid(pts) { const s = pts.reduce((a, p) => add(a, p), [0, 0]); return mul(s, 1 / pts.length); }
function dot(a, b) { return a[0] * b[0] + a[1] * b[1]; }
function P(pts) { return pts.map(p => `${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' '); }
function ngon(c, r, n, rot = 0, ry = null) {
  const out = [];
  for (let i = 0; i < n; i++) {
    const a = rot + i * 2 * Math.PI / n;
    out.push([c[0] + Math.cos(a) * r, c[1] + Math.sin(a) * (ry == null ? r : ry)]);
  }
  return out;
}
const INK = '#17120f';
function poly(pts, fill, extra = '') { return `<polygon points="${P(pts)}" fill="${fill}" ${extra}/>`; }
function outline(pts, w = 8) { return poly(pts, INK, `stroke="${INK}" stroke-width="${w}" stroke-linejoin="round"`); }
function facet(pts, base, opts = {}) {
  const c = opts.c || centroid(pts);
  const amt = opts.amt == null ? 0.42 : opts.amt;
  let s = '';
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i], b = pts[(i + 1) % pts.length];
    const d = dot(norm(sub(lerp(a, b, 0.5), c)), LIGHT);
    s += poly([c, a, b], shade(base, d * amt), `stroke="${shade(base, d * amt)}" stroke-width="0.6" stroke-linejoin="round"`);
  }
  return s;
}
function gem(pts, base, opts = {}) { return outline(pts, opts.w || 8) + facet(pts, base, opts); }
function sparkle(c, r, color = '#ffffff', op = 1) {
  const [x, y] = c, k = r * 0.22;
  return `<path d="M${x} ${y - r} L${x + k} ${y - k} L${x + r} ${y} L${x + k} ${y + k} L${x} ${y + r} L${x - k} ${y + k} L${x - r} ${y} L${x - k} ${y - k} Z" fill="${color}" opacity="${op}"/>`;
}
function glow(c, r, color, op = 0.6, id) {
  return `<defs><radialGradient id="${id}"><stop offset="0" stop-color="${color}" stop-opacity="${op}"/><stop offset="1" stop-color="${color}" stop-opacity="0"/></radialGradient></defs><circle cx="${c[0]}" cy="${c[1]}" r="${r}" fill="url(#${id})"/>`;
}
function iconFrame(name, bg, inner) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<defs><radialGradient id="bg_${name}" cx="50%" cy="42%" r="70%"><stop offset="0" stop-color="${shade(bg, 0.12)}"/><stop offset="0.65" stop-color="${shade(bg, -0.35)}"/><stop offset="1" stop-color="${shade(bg, -0.7)}"/></radialGradient></defs>
<rect x="0" y="0" width="256" height="256" fill="#2f2b27"/>
<rect x="10" y="10" width="236" height="236" fill="url(#bg_${name})"/>
<rect x="10" y="10" width="236" height="236" fill="none" stroke="#b7b0a5" stroke-width="3"/>
<path d="M1 1 L12 12 M255 1 L244 12 M1 255 L12 244 M255 255 L244 244" stroke="#b7b0a5" stroke-width="3"/>
${inner}
</svg>`;
}

const GOLD = '#e8b54a', STEEL_BLUE = '#5b7aa6', BG_WAR = '#6a2219', BG_ROGUE = '#174535', BG_ITEM = '#4a4237';
const icons = [];

// 1. Oathkeeper Shield: a round steel shield with a gold rim, cross bands and a boss
icons.push({ name: 'OathkeeperShield', svg: iconFrame('OathkeeperShield', BG_WAR, (() => {
  const c = [128, 130];
  let s = glow(c, 118, '#ffd36a', 0.45, 'oks_g');
  s += gem(ngon(c, 98, 18, 0.17), GOLD, { amt: 0.4, w: 9 });
  s += facet(ngon(c, 84, 18, 0.17), STEEL_BLUE, { c, amt: 0.45 });
  // cross bands
  s += outline([[118, 48], [138, 48], [138, 212], [118, 212]], 6) + poly([[118, 48], [128, 48], [128, 212], [118, 212]], shade(GOLD, 0.3)) + poly([[128, 48], [138, 48], [138, 212], [128, 212]], shade(GOLD, -0.2));
  s += outline([[46, 120], [210, 120], [210, 140], [46, 140]], 6) + poly([[46, 120], [210, 120], [210, 130], [46, 130]], shade(GOLD, 0.3)) + poly([[46, 130], [210, 130], [210, 140], [46, 140]], shade(GOLD, -0.2));
  // boss
  s += gem(ngon(c, 26, 10, 0.3), GOLD, { amt: 0.5, w: 7 });
  s += facet(ngon([122, 124], 9, 6, 0.2), '#fff2c0', { amt: 0.2 });
  // rivets
  for (let i = 0; i < 8; i++) {
    const a = i * Math.PI / 4 + Math.PI / 8;
    s += `<circle cx="${(c[0] + Math.cos(a) * 90).toFixed(1)}" cy="${(c[1] + Math.sin(a) * 90).toFixed(1)}" r="4.5" fill="${shade(GOLD, -0.35)}" stroke="${INK}" stroke-width="2"/>`;
  }
  s += sparkle([196, 64], 14, '#fff3c4') + sparkle([62, 196], 9, '#ffe08a');
  return s;
})()) });

// 2. Shadow Cloak: a hooded cloak in deep violet, an empty dark hood and a silver clasp
icons.push({ name: 'ShadowCloak', svg: iconFrame('ShadowCloak', BG_ROGUE, (() => {
  let s = glow([128, 132], 120, '#9a6cff', 0.5, 'sc_g');
  const cloak = [[128, 34], [160, 52], [174, 86], [196, 120], [214, 214], [176, 222], [128, 214], [80, 222], [42, 214], [60, 120], [82, 86], [96, 52]];
  s += gem(cloak, '#3a2466', { c: [128, 120], amt: 0.45, w: 9 });
  // inner lining down the front
  s += facet([[128, 120], [146, 130], [160, 214], [128, 206], [96, 214], [110, 130]], '#5b3a96', { amt: 0.35 });
  // folds
  for (const [x1, y1, x2, y2] of [[88, 140, 66, 212], [168, 140, 190, 212], [116, 150, 110, 206], [140, 150, 146, 206]]) {
    s += `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${INK}" stroke-opacity="0.55" stroke-width="4" stroke-linecap="round"/>`;
  }
  // hood opening: darkness with two faint eyes
  s += `<ellipse cx="128" cy="82" rx="24" ry="30" fill="#07050c" stroke="${INK}" stroke-width="5"/>`;
  s += `<ellipse cx="119" cy="84" rx="4" ry="2.4" fill="#b99cff" opacity="0.85"/><ellipse cx="137" cy="84" rx="4" ry="2.4" fill="#b99cff" opacity="0.85"/>`;
  // silver clasp
  s += gem(ngon([128, 120], 11, 8, 0.4), '#cfd6e2', { amt: 0.45, w: 5 });
  // shadow wisps
  for (const [x, y, r] of [[52, 72, 10], [208, 70, 8], [40, 160, 7], [218, 168, 9]]) {
    s += `<circle cx="${x}" cy="${y}" r="${r}" fill="#6d4ad1" opacity="0.45"/>`;
  }
  s += sparkle([204, 52], 12, '#e6d8ff');
  return s;
})()) });

// 3. Boots of Striding: a pair of leather boots with gold cuffs and little wings
function boot(dx, dy, base) {
  const p = (x, y) => [x + dx, y + dy];
  let s = '';
  const shape = [p(82, 66), p(126, 66), p(126, 160), p(172, 178), p(180, 198), p(176, 212), p(78, 212), p(76, 150)];
  s += gem(shape, base, { c: p(110, 150), amt: 0.42, w: 9 });
  // sole
  s += outline([p(76, 204), p(178, 204), p(176, 214), p(78, 214)], 5) + poly([p(76, 204), p(178, 204), p(176, 214), p(78, 214)], '#3a2414');
  // gold cuff
  s += gem([p(78, 60), p(130, 60), p(130, 80), p(78, 80)], GOLD, { amt: 0.35, w: 6 });
  // wing on the cuff
  const w0 = p(130, 70);
  s += gem([w0, p(162, 44), p(170, 54), p(158, 62), p(166, 70), p(150, 78)], '#f3e6bd', { amt: 0.3, w: 5 });
  // laces
  for (let i = 0; i < 3; i++) s += `<line x1="${dx + 96}" y1="${dy + 96 + i * 18}" x2="${dx + 116}" y2="${dy + 104 + i * 18}" stroke="${GOLD}" stroke-width="4" stroke-linecap="round"/>`;
  return s;
}
icons.push({ name: 'BootsOfStriding', svg: iconFrame('BootsOfStriding', BG_ITEM, (() => {
  let s = glow([128, 136], 116, '#ffd36a', 0.4, 'bs_g');
  s += boot(30, -8, '#6b3f22');
  s += boot(-22, 10, '#8a5330');
  // speed lines
  for (const [y, len] of [[150, 40], [172, 56], [194, 34]]) {
    s += `<line x1="${22}" y1="${y}" x2="${22 + len}" y2="${y}" stroke="#fff3c4" stroke-opacity="0.7" stroke-width="5" stroke-linecap="round"/>`;
  }
  s += sparkle([206, 58], 12, '#fff3c4') + sparkle([214, 150], 8, '#ffe08a');
  return s;
})()) });

for (const ic of icons) {
  const html = `<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;padding:0;background:transparent;overflow:hidden}svg{display:block}</style></head><body>${ic.svg}</body></html>`;
  fs.writeFileSync(`icon_${ic.name}.html`, html);
}
console.log(icons.map(i => i.name).join(', '));
