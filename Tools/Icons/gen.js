// Castle of Dice icon generator: low-poly faceted icons in the style of CoinIcon / HealthPotionIcon.
// Writes sheet.html (a grid of 256x256 SVG icons) and order.json (names in grid order).
const fs = require('fs');

// ---------- colour helpers ----------
function hexToRgb(h) { h = h.replace('#', ''); return [0, 2, 4].map(i => parseInt(h.substr(i, 2), 16)); }
function rgbToHex(r) { return '#' + r.map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join(''); }
function shade(h, t) {
  const c = hexToRgb(h);
  return rgbToHex(t >= 0 ? c.map(v => v + (255 - v) * t) : c.map(v => v * (1 + t)));
}

// ---------- geometry helpers ----------
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
function frameAxis(base, tip) { const u = norm(sub(tip, base)); return { u, v: [-u[1], u[0]] }; }
function at(p, ax, a, b) { return add(add(p, mul(ax.u, a)), mul(ax.v, b)); }

const INK = '#17120f';

function poly(pts, fill, extra = '') { return `<polygon points="${P(pts)}" fill="${fill}" ${extra}/>`; }
function outline(pts, w = 8) { return poly(pts, INK, `stroke="${INK}" stroke-width="${w}" stroke-linejoin="round"`); }

// Faceted (low-poly) fill: a fan of triangles from the centre, each lit by its outward direction.
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
// Outline + facets in one go
function gem(pts, base, opts = {}) { return outline(pts, opts.w || 8) + facet(pts, base, opts); }

// A two-tone bevelled strip (blade, handle): light on the left of the axis, dark on the right
function bevel(base, tip, wBase, wTip, color, opts = {}) {
  const ax = frameAxis(base, tip);
  const L = [at(base, ax, 0, -wBase), at(tip, ax, 0, -wTip)];
  const R = [at(base, ax, 0, wBase), at(tip, ax, 0, wTip)];
  const all = opts.point ? [L[0], at(tip, ax, 0, 0), R[0]] : [L[0], L[1], R[1], R[0]];
  let s = outline(all, opts.w || 7);
  if (opts.point) {
    s += poly([L[0], at(tip, ax, 0, 0), base], shade(color, 0.35));
    s += poly([R[0], at(tip, ax, 0, 0), base], shade(color, -0.25));
  } else {
    s += poly([L[0], L[1], tip, base], shade(color, 0.3));
    s += poly([R[0], R[1], tip, base], shade(color, -0.25));
  }
  return s;
}

function sparkle(c, r, color = '#ffffff', op = 1) {
  const [x, y] = c, k = r * 0.22;
  return `<path d="M${x} ${y - r} L${x + k} ${y - k} L${x + r} ${y} L${x + k} ${y + k} L${x} ${y + r} L${x - k} ${y + k} L${x - r} ${y} L${x - k} ${y - k} Z" fill="${color}" opacity="${op}"/>`;
}
function glow(c, r, color, op = 0.6, id) {
  return `<defs><radialGradient id="${id}"><stop offset="0" stop-color="${color}" stop-opacity="${op}"/><stop offset="1" stop-color="${color}" stop-opacity="0"/></radialGradient></defs><circle cx="${c[0]}" cy="${c[1]}" r="${r}" fill="url(#${id})"/>`;
}

// ---------- frames ----------
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
function crestFrame(name, bg, inner) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<defs><linearGradient id="cb_${name}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${shade(bg, 0.15)}"/><stop offset="1" stop-color="${shade(bg, -0.55)}"/></linearGradient>
<linearGradient id="cg_${name}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ffe7a0"/><stop offset="0.5" stop-color="#d9a640"/><stop offset="1" stop-color="#8a5f1c"/></linearGradient></defs>
<rect x="6" y="6" width="244" height="244" rx="34" fill="${INK}"/>
<rect x="12" y="12" width="232" height="232" rx="30" fill="url(#cg_${name})"/>
<rect x="26" y="26" width="204" height="204" rx="20" fill="url(#cb_${name})"/>
<rect x="26" y="26" width="204" height="204" rx="20" fill="none" stroke="${INK}" stroke-opacity="0.5" stroke-width="3"/>
${inner}
</svg>`;
}

// ---------- reusable parts ----------
const STEEL = '#b9c4cf', GOLD = '#e8b54a', LEATHER = '#7b4a2a', WOOD = '#8a5a32';

function sword(base, tip, o = {}) {
  const ax = frameAxis(base, tip);
  const len = Math.hypot(tip[0] - base[0], tip[1] - base[1]);
  const guardAt = o.guardAt || 0.30;
  const g = lerp(base, tip, guardAt);
  const bw = o.bladeW || 13;
  let s = '';
  // grip and pommel
  s += bevel(base, g, 7, 7, o.grip || LEATHER);
  const pom = at(base, ax, -6, 0);
  s += gem(ngon(pom, o.pommel || 13, 6, 0.3), GOLD);
  // blade
  const bladeStart = at(g, ax, 6, 0);
  const bl = [at(bladeStart, ax, 0, -bw), at(tip, ax, -bw * 2.2, -bw * 0.8), tip, at(tip, ax, -bw * 2.2, bw * 0.8), at(bladeStart, ax, 0, bw)];
  s += outline(bl, 8);
  s += poly([bl[0], bl[1], tip, bladeStart], shade(o.blade || STEEL, 0.38));
  s += poly([bl[4], bl[3], tip, bladeStart], shade(o.blade || STEEL, -0.22));
  // fuller line
  s += `<line x1="${at(bladeStart, ax, 8, 0)[0]}" y1="${at(bladeStart, ax, 8, 0)[1]}" x2="${at(tip, ax, -bw * 3, 0)[0]}" y2="${at(tip, ax, -bw * 3, 0)[1]}" stroke="${shade(o.blade || STEEL, -0.45)}" stroke-width="3" stroke-linecap="round"/>`;
  // crossguard
  const gw = o.guardW || 34;
  const gq = [at(g, ax, -6, -gw), at(g, ax, 6, -gw), at(g, ax, 6, gw), at(g, ax, -6, gw)];
  s += gem(gq, GOLD, { amt: 0.35 });
  s += gem(ngon(at(g, ax, 0, 0), 7, 4, Math.PI / 4), '#c0392b', { w: 5 });
  return s;
}

function dagger(base, tip, o = {}) {
  return sword(base, tip, Object.assign({ guardAt: 0.36, bladeW: 11, guardW: 22, pommel: 10 }, o));
}

function puff(c, r, color) {
  // faceted cloud puff: lit from the top-left
  return gem(ngon(c, r, 9, 0.2), color, { amt: 0.35, w: 7 });
}

function scroll(seal, sealColor, accent) {
  let s = '';
  // sheet
  const sheet = [[74, 64], [182, 64], [178, 194], [78, 194]];
  s += outline(sheet, 8);
  s += poly(sheet, '#ead9a8');
  s += poly([[74, 64], [128, 64], [128, 194], [78, 194]], '#f3e6bd');
  // text lines
  for (let y = 92; y <= 170; y += 16) s += `<line x1="92" y1="${y}" x2="${y > 150 ? 140 : 164}" y2="${y}" stroke="#b49a63" stroke-width="4" stroke-linecap="round"/>`;
  // rolls
  for (const y of [60, 198]) {
    const roll = [[62, y - 11], [194, y - 11], [194, y + 11], [62, y + 11]];
    s += outline(roll, 8);
    s += poly([[62, y - 11], [194, y - 11], [194, y], [62, y]], '#f6ebc8');
    s += poly([[62, y], [194, y], [194, y + 11], [62, y + 11]], '#c9b07a');
    s += gem(ngon([62, y], 11, 6), '#a8875a', { w: 6 });
    s += gem(ngon([194, y], 11, 6), '#a8875a', { w: 6 });
  }
  // wax seal
  s += glow([150, 168], 46, accent, 0.55, 'sg' + seal);
  s += gem(ngon([150, 168], 26, 10, 0.1), sealColor, { amt: 0.45 });
  s += seal === 'ward'
    ? `<polygon points="${P(ngon([150, 168], 13, 6, Math.PI / 6))}" fill="none" stroke="#f1e2ff" stroke-width="4"/><circle cx="150" cy="168" r="4" fill="#f1e2ff"/>`
    : `<path d="M150 150 C 160 162 162 170 156 180 C 152 186 146 186 143 180 C 139 172 146 168 150 150 Z" fill="#ffd36a" stroke="#fff3c4" stroke-width="2"/>`;
  return s;
}

function flask(neckTop, bodyPts, liquidPts, glass, liquid, cork) {
  let s = '';
  s += outline(bodyPts, 9);
  s += facet(bodyPts, glass, { amt: 0.25 });
  s += facet(liquidPts, liquid, { amt: 0.45 });
  // glass highlight
  s += `<path d="M${bodyPts[1][0] + 10} ${bodyPts[1][1] + 18} Q ${bodyPts[1][0] - 2} ${bodyPts[1][1] + 50} ${bodyPts[1][0] + 8} ${bodyPts[1][1] + 80}" stroke="#ffffff" stroke-opacity="0.7" stroke-width="6" fill="none" stroke-linecap="round"/>`;
  s += gem(cork, '#a8754a', { amt: 0.3, w: 7 });
  return s;
}

// ---------- icons ----------
const icons = [];
function icon(name, label, svg) { icons.push({ name, label, svg }); }

const BG_WAR = '#6a2219', BG_MAGE = '#2c2569', BG_ROGUE = '#174535', BG_ITEM = '#4a4237', BG_HUD = '#433a2c';

// Warrior
icon('Sword', 'Sword Slash', iconFrame('Sword', BG_WAR,
  `<defs><linearGradient id="sl" x1="0" y1="1" x2="1" y2="0"><stop offset="0" stop-color="#ffffff" stop-opacity="0"/><stop offset="0.6" stop-color="#fff3d0" stop-opacity="0.85"/><stop offset="1" stop-color="#ffffff" stop-opacity="0.2"/></linearGradient></defs>
  <path d="M44 196 Q 58 70 214 42 Q 92 92 44 196 Z" fill="url(#sl)"/>
  <path d="M60 206 Q 80 104 214 70 Q 112 120 60 206 Z" fill="url(#sl)" opacity="0.5"/>
  ${sword([66, 196], [204, 58])}
  ${sparkle([206, 54], 16)}`));

icon('Shield', 'Shield Block', iconFrame('Shield', BG_WAR, (() => {
  const outer = [[62, 50], [194, 50], [198, 116], [128, 220], [58, 116]];
  const c = [128, 112];
  const inner = outer.map(p => lerp(c, p, 0.8));
  return `${glow([128, 120], 110, '#ffcf6a', 0.25, 'shg')}${gem(outer, GOLD, { c })}${facet(inner, '#9fb0c0', { c, amt: 0.4 })}
  <polygon points="${P(inner)}" fill="none" stroke="${INK}" stroke-opacity="0.5" stroke-width="3"/>
  ${gem([[120, 72], [136, 72], [136, 102], [164, 102], [164, 118], [136, 118], [136, 176], [120, 176], [120, 118], [92, 118], [92, 102], [120, 102]], GOLD, { w: 5, amt: 0.3, c: [128, 110] })}
  ${sparkle([194, 56], 15)}${sparkle([62, 128], 9, '#fff3c4', 0.9)}`;
})()));

icon('WarCry', 'War Cry', iconFrame('WarCry', BG_WAR, (() => {
  // curved horn along a quadratic curve, widening to the bell
  const p0 = [50, 200], p1 = [74, 100], p2 = [160, 86];
  const q = t => lerp(lerp(p0, p1, t), lerp(p1, p2, t), t);
  const N = 9; let L = [], R = [];
  for (let i = 0; i <= N; i++) {
    const t = i / N, p = q(t), d = norm(sub(q(Math.min(1, t + 0.01)), q(Math.max(0, t - 0.01))));
    const v = [-d[1], d[0]], w = 6 + 34 * t * t;
    L.push(add(p, mul(v, -w))); R.push(add(p, mul(v, w)));
  }
  let s = glow([170, 88], 70, '#ffe08a', 0.35, 'wcg');
  s += outline(L.concat(R.slice().reverse()), 9);
  for (let i = 0; i < N; i++) {
    const t = (i + 0.5) / N, band = (t > 0.32 && t < 0.42) || (t > 0.7 && t < 0.8);
    const base = band ? GOLD : '#efe2c2';
    s += poly([L[i], L[i + 1], q((i + 1) / N), q(i / N)], shade(base, 0.25));
    s += poly([q(i / N), q((i + 1) / N), R[i + 1], R[i]], shade(base, -0.25));
  }
  const bellC = q(1);
  s += `<ellipse cx="${bellC[0]}" cy="${bellC[1]}" rx="9" ry="40" fill="#3a2414" stroke="${INK}" stroke-width="5" transform="rotate(-8 ${bellC[0]} ${bellC[1]})"/>`;
  for (let k = 0; k < 3; k++) {
    const r = 20 + k * 15;
    s += `<path d="M${178 + k * 5} ${88 - r} A ${r} ${r} 0 0 1 ${178 + k * 5} ${88 + r}" fill="none" stroke="#fff3c4" stroke-opacity="${0.95 - k * 0.25}" stroke-width="${7 - k}" stroke-linecap="round"/>`;
  }
  return s;
})()));

icon('IronWill', 'Iron Will', iconFrame('IronWill', BG_WAR, (() => {
  let s = glow([128, 128], 112, '#ffd66b', 0.6, 'iwg');
  for (let i = 0; i < 12; i++) {
    const a = i * Math.PI / 6, r1 = 74, r2 = 104;
    s += `<line x1="${128 + Math.cos(a) * r1}" y1="${128 + Math.sin(a) * r1}" x2="${128 + Math.cos(a) * r2}" y2="${128 + Math.sin(a) * r2}" stroke="#ffe7a0" stroke-opacity="0.65" stroke-width="5" stroke-linecap="round"/>`;
  }
  const helm = [[86, 62], [170, 62], [186, 104], [182, 196], [74, 196], [70, 104]];
  s += gem(helm, STEEL, { c: [128, 120] });
  s += poly([[80, 118], [176, 118], [176, 134], [80, 134]], '#1c1a22');
  s += `<rect x="122" y="62" width="12" height="134" fill="${GOLD}" stroke="${INK}" stroke-width="3"/>`;
  for (let y = 150; y <= 182; y += 14) for (const x of [96, 108, 148, 160]) s += `<circle cx="${x}" cy="${y}" r="3.5" fill="#2a2730"/>`;
  s += sparkle([186, 64], 13) + sparkle([66, 186], 9, '#fff3c4');
  return s;
})()));

icon('Retaliation', 'Retaliation', iconFrame('Retaliation', BG_WAR, (() => {
  let s = '';
  s += sword([200, 204], [62, 66], { blade: '#d8dee6' });
  const c = [120, 146];
  s += gem(ngon(c, 58, 10, 0.15), GOLD, { amt: 0.35 });
  s += facet(ngon(c, 46, 10, 0.15), '#9fb0c0', { amt: 0.42 });
  s += gem(ngon(c, 13, 6, 0), GOLD, { w: 5 });
  s += `<path d="M196 168 A 84 84 0 0 1 60 92" fill="none" stroke="${INK}" stroke-width="16" stroke-linecap="round"/>`;
  s += `<path d="M196 168 A 84 84 0 0 1 60 92" fill="none" stroke="#e8463a" stroke-width="9" stroke-linecap="round"/>`;
  s += gem([[44, 80], [80, 74], [62, 110]], '#e8463a', { w: 6, amt: 0.3 });
  return s;
})()));

// Mage
icon('Frostbite', 'Frostbite', iconFrame('Frostbite', BG_MAGE, (() => {
  let s = glow([128, 128], 112, '#8fe3ff', 0.5, 'fbg');
  const base = [60, 198], tip = [204, 54], ax = frameAxis(base, tip);
  const shard = [base, at(base, ax, 60, -24), at(base, ax, 150, -14), tip, at(base, ax, 150, 14), at(base, ax, 60, 24)];
  s += gem(shard, '#9fe6ff', { amt: 0.5 });
  s += `<polyline points="${P([base, at(base, ax, 60, 0), at(base, ax, 150, 0), tip])}" fill="none" stroke="#ffffff" stroke-opacity="0.6" stroke-width="3"/>`;
  for (const [cx, cy, r] of [[86, 92, 16], [178, 172, 13], [64, 140, 9], [150, 210, 8]]) {
    s += gem(ngon([cx, cy], r, 4, Math.PI / 4), '#cdf4ff', { w: 5, amt: 0.4 });
  }
  s += sparkle([206, 52], 17) + sparkle([110, 64], 9, '#e8fbff');
  return s;
})()));

icon('Fireball', 'Fireball', iconFrame('Fireball', BG_MAGE, (() => {
  let s = glow([140, 112], 112, '#ff9a3a', 0.65, 'fig');
  const c = [146, 108];
  const flames = [
    [[110, 128], [48, 206], [124, 158]], [[126, 146], [74, 220], [150, 160]], [[104, 102], [36, 152], [116, 128]],
    [[144, 156], [116, 214], [166, 156]]];
  for (const f of flames) s += gem(f, '#ff6a1f', { w: 7, amt: 0.35 });
  s += gem(ngon(c, 54, 10, 0.2), '#ff8a2a', { amt: 0.45 });
  s += facet(ngon([138, 100], 32, 8, 0.4), '#ffd45a', { amt: 0.35 });
  s += facet(ngon([132, 94], 14, 6, 0.2), '#fff6c8', { amt: 0.2 });
  s += sparkle([196, 64], 13, '#fff3c4') + sparkle([206, 150], 8, '#ffd45a');
  return s;
})()));

icon('ManaShield', 'Mana Shield', iconFrame('ManaShield', BG_MAGE, (() => {
  let s = glow([128, 128], 116, '#a77bff', 0.6, 'msg');
  const hex = ngon([128, 128], 84, 6, Math.PI / 6);
  s += outline(hex, 9);
  s += `<g opacity="0.88">${facet(hex, '#8a63e8', { amt: 0.5 })}</g>`;
  s += `<polygon points="${P(ngon([128, 128], 62, 6, Math.PI / 6))}" fill="none" stroke="#e6d8ff" stroke-width="4"/>`;
  s += `<circle cx="128" cy="128" r="26" fill="none" stroke="#f4eeff" stroke-width="5"/>`;
  for (let i = 0; i < 6; i++) {
    const a = Math.PI / 6 + i * Math.PI / 3;
    s += `<line x1="${128 + Math.cos(a) * 26}" y1="${128 + Math.sin(a) * 26}" x2="${128 + Math.cos(a) * 60}" y2="${128 + Math.sin(a) * 60}" stroke="#e6d8ff" stroke-width="3"/>`;
  }
  s += sparkle([128, 128], 14) + sparkle([186, 66], 12, '#f4eeff');
  return s;
})()));

icon('Blink', 'Blink', iconFrame('Blink', BG_MAGE, (() => {
  let s = '';
  s += `<ellipse cx="82" cy="170" rx="38" ry="14" fill="none" stroke="#b28cff" stroke-opacity="0.5" stroke-width="6"/>`;
  s += `<ellipse cx="82" cy="170" rx="22" ry="7" fill="none" stroke="#b28cff" stroke-opacity="0.35" stroke-width="4"/>`;
  s += `<path d="M84 160 Q 110 70 170 92" fill="none" stroke="#e6d8ff" stroke-opacity="0.8" stroke-width="5" stroke-dasharray="10 9" stroke-linecap="round"/>`;
  s += glow([176, 110], 70, '#8fe3ff', 0.6, 'blg');
  s += `<ellipse cx="176" cy="128" rx="46" ry="18" fill="none" stroke="${INK}" stroke-width="12"/>`;
  s += `<ellipse cx="176" cy="128" rx="46" ry="18" fill="none" stroke="#8fe3ff" stroke-width="7"/>`;
  s += `<ellipse cx="176" cy="128" rx="28" ry="10" fill="none" stroke="#d6f6ff" stroke-width="4"/>`;
  s += gem([[176, 54], [190, 96], [176, 112], [162, 96]], '#d6f6ff', { w: 6, amt: 0.35 });
  s += sparkle([176, 84], 22) + sparkle([118, 74], 10, '#e6d8ff') + sparkle([214, 176], 9, '#d6f6ff');
  return s;
})()));

icon('ArcaneChains', 'Arcane Chains', iconFrame('ArcaneChains', BG_MAGE, (() => {
  let s = glow([128, 128], 110, '#a77bff', 0.5, 'acg');
  const links = [[[78, 178], 45], [[128, 128], -45], [[178, 78], 45]];
  for (const [[x, y], rot] of links) {
    s += `<g transform="rotate(${rot} ${x} ${y})">
      <rect x="${x - 40}" y="${y - 20}" width="80" height="40" rx="20" fill="none" stroke="${INK}" stroke-width="22"/>
      <rect x="${x - 40}" y="${y - 20}" width="80" height="40" rx="20" fill="none" stroke="#9a72f0" stroke-width="14"/>
      <rect x="${x - 40}" y="${y - 20}" width="80" height="40" rx="20" fill="none" stroke="#d8c6ff" stroke-width="4" stroke-dasharray="20 40"/>
    </g>`;
  }
  for (const [x, y] of [[62, 90], [198, 168], [112, 208]]) s += `<path d="M${x - 7} ${y - 12} L ${x} ${y} L ${x + 7} ${y - 12} M${x} ${y} L ${x} ${y + 14}" stroke="#e6d8ff" stroke-width="4" stroke-linecap="round" fill="none" opacity="0.9"/>`;
  s += sparkle([206, 52], 14, '#f4eeff') + sparkle([52, 206], 10, '#d8c6ff');
  return s;
})()));

// Rogue
icon('PoisonDagger', 'Poison Dagger', iconFrame('PoisonDagger', BG_ROGUE, (() => {
  let s = glow([150, 100], 90, '#7dff5a', 0.35, 'pdg');
  s += dagger([72, 190], [196, 64], { blade: '#b8e6a8' });
  for (const [x, y, r] of [[170, 120, 9], [184, 150, 7], [156, 160, 6]]) {
    s += `<path d="M${x} ${y - r * 2} Q ${x + r} ${y - r * 0.2} ${x} ${y + r} Q ${x - r} ${y - r * 0.2} ${x} ${y - r * 2} Z" fill="#6fdc4a" stroke="${INK}" stroke-width="3"/>`;
  }
  s += sparkle([200, 60], 13, '#d8ffc8');
  return s;
})()));

icon('Backstab', 'Backstab', iconFrame('Backstab', BG_ROGUE, (() => {
  let s = '';
  s += `<path d="M150 40 A 70 70 0 1 0 216 150 A 56 56 0 1 1 150 40 Z" fill="#cfd8e6" opacity="0.25"/>`;
  s += dagger([184, 54], [76, 196], { blade: '#d8dee6' });
  s += `<path d="M58 132 L 120 120 M70 168 L 132 150 M96 206 L 150 178" stroke="#e8463a" stroke-width="8" stroke-linecap="round"/>`;
  s += `<path d="M58 132 L 120 120 M70 168 L 132 150 M96 206 L 150 178" stroke="#ff9a8a" stroke-width="3" stroke-linecap="round"/>`;
  s += sparkle([76, 198], 16, '#ffd0c8');
  return s;
})()));

icon('SmokeBomb', 'Smoke Bomb', iconFrame('SmokeBomb', BG_ROGUE, (() => {
  let s = '';
  for (const [x, y, r] of [[176, 82, 34], [196, 126, 28], [150, 58, 26], [204, 78, 22]]) s += puff([x, y], r, '#9aa3ad');
  const c = [114, 150];
  s += gem(ngon(c, 56, 10, 0.1), '#3a3d4a', { amt: 0.5 });
  s += facet(ngon([100, 134], 16, 6, 0.3), '#7a8094', { amt: 0.3 });
  s += gem([[136, 96], [160, 104], [154, 122], [130, 114]], '#8a8f9c', { w: 6 });
  s += `<path d="M152 104 Q 170 84 160 66" stroke="${INK}" stroke-width="9" fill="none" stroke-linecap="round"/><path d="M152 104 Q 170 84 160 66" stroke="#c9a46a" stroke-width="5" fill="none" stroke-linecap="round"/>`;
  s += glow([160, 62], 30, '#ffd45a', 0.8, 'sbg') + sparkle([160, 62], 16, '#fff3c4');
  return s;
})()));

icon('ShadowStep', 'Shadow Step', iconFrame('ShadowStep', BG_ROGUE, (() => {
  let s = glow([160, 92], 90, '#a77bff', 0.45, 'ssg');
  function boot(x, y, rot, color, op) {
    return `<g transform="translate(${x} ${y}) rotate(${rot}) scale(1.25)" opacity="${op}">
      <path d="M -15 -28 C -17 -48 17 -48 15 -28 C 14 -14 9 -4 8 4 L -8 4 C -9 -4 -14 -14 -15 -28 Z" fill="${color}" stroke="${INK}" stroke-width="4"/>
      <path d="M -11 12 C -13 30 13 30 11 12 Z" fill="${color}" stroke="${INK}" stroke-width="4"/></g>`;
  }
  s += boot(80, 176, -30, '#6b4fa8', 0.45);
  s += `<path d="M98 150 Q 120 110 150 106" fill="none" stroke="#c8b0ff" stroke-width="5" stroke-dasharray="9 9" stroke-linecap="round" opacity="0.8"/>`;
  s += boot(170, 96, 20, '#2a1f45', 1);
  s += `<path d="M150 60 Q 190 40 212 70" fill="none" stroke="#c8b0ff" stroke-width="5" stroke-linecap="round"/><path d="M196 140 Q 222 120 214 96" fill="none" stroke="#c8b0ff" stroke-width="4" stroke-linecap="round" opacity="0.7"/>`;
  s += sparkle([206, 58], 12, '#e6d8ff');
  return s;
})()));

icon('PoisonCloud', 'Poison Cloud', iconFrame('PoisonCloud', BG_ROGUE, (() => {
  let s = glow([128, 136], 110, '#7dff5a', 0.45, 'pcg');
  for (const [x, y, r] of [[92, 150, 38], [164, 150, 40], [128, 112, 46], [128, 170, 36], [70, 118, 22], [190, 110, 24]]) s += puff([x, y], r, '#69c94a');
  // skull
  s += `<path d="M128 112 C 104 112 98 132 104 146 L 108 158 L 148 158 L 152 146 C 158 132 152 112 128 112 Z" fill="#eef5e6" stroke="${INK}" stroke-width="5"/>`;
  s += `<rect x="114" y="156" width="28" height="12" rx="3" fill="#eef5e6" stroke="${INK}" stroke-width="4"/>`;
  s += `<circle cx="117" cy="136" r="8" fill="#1b2a16"/><circle cx="139" cy="136" r="8" fill="#1b2a16"/><path d="M128 144 l -4 8 h 8 z" fill="#1b2a16"/>`;
  for (const [x, y, r] of [[60, 78, 7], [196, 66, 9], [208, 190, 6], [52, 192, 8]]) s += `<circle cx="${x}" cy="${y}" r="${r}" fill="#b8ff9a" stroke="${INK}" stroke-width="3"/>`;
  return s;
})()));

icon('Lockpicking', 'Lockpicking', iconFrame('Lockpicking', BG_ROGUE, (() => {
  let s = '';
  s += `<path d="M96 116 L 96 86 A 32 32 0 0 1 160 86 L 160 116" fill="none" stroke="${INK}" stroke-width="24"/>`;
  s += `<path d="M96 116 L 96 86 A 32 32 0 0 1 160 86 L 160 116" fill="none" stroke="#c9d2dc" stroke-width="14"/>`;
  s += `<path d="M92 112 L 92 86 A 36 36 0 0 1 106 58" fill="none" stroke="#ffffff" stroke-opacity="0.5" stroke-width="4"/>`;
  const body = [[78, 112], [178, 112], [184, 140], [178, 204], [78, 204], [72, 140]];
  s += gem(body, GOLD, { c: [128, 150] });
  s += `<circle cx="128" cy="146" r="12" fill="#2a1d0d"/><path d="M121 150 L 135 150 L 131 178 L 125 178 Z" fill="#2a1d0d"/>`;
  s += `<path d="M128 156 L 206 110" stroke="${INK}" stroke-width="10" stroke-linecap="round"/><path d="M128 156 L 206 110" stroke="#dfe6ee" stroke-width="5" stroke-linecap="round"/>`;
  s += `<path d="M128 160 L 204 196" stroke="${INK}" stroke-width="10" stroke-linecap="round"/><path d="M128 160 L 204 196" stroke="#dfe6ee" stroke-width="5" stroke-linecap="round"/>`;
  s += gem([[200, 100], [222, 104], [218, 124], [196, 120]], '#7b4a2a', { w: 5 });
  s += gem([[198, 190], [220, 192], [214, 212], [194, 208]], '#7b4a2a', { w: 5 });
  s += sparkle([76, 108], 12, '#fff3c4');
  return s;
})()));

// Items
icon('PoisonVial', 'Poison Vial', iconFrame('PoisonVial', BG_ITEM, (() => {
  let s = glow([128, 150], 90, '#7dff5a', 0.4, 'pvg');
  const body = [[112, 82], [144, 82], [146, 116], [176, 150], [176, 192], [152, 212], [104, 212], [80, 192], [80, 150], [110, 116]];
  const liquid = [[88, 156], [168, 156], [170, 190], [150, 206], [106, 206], [86, 190]];
  s += flask(null, body, liquid, '#cfe8f0', '#58c83a', [[108, 56], [148, 56], [146, 84], [110, 84]]);
  s += `<path d="M128 166 C 116 166 112 176 116 184 L 118 192 L 138 192 L 140 184 C 144 176 140 166 128 166 Z" fill="#eef5e6" stroke="${INK}" stroke-width="3"/><circle cx="122" cy="178" r="4" fill="#1b2a16"/><circle cx="134" cy="178" r="4" fill="#1b2a16"/>`;
  for (const [x, y, r] of [[118, 146, 5], [140, 138, 4], [130, 124, 3]]) s += `<circle cx="${x}" cy="${y}" r="${r}" fill="#b8ff9a"/>`;
  return s;
})()));

icon('RuneOfFate', 'Rune of Fate', iconFrame('RuneOfFate', BG_ITEM, (() => {
  let s = glow([128, 128], 106, '#5ad1ff', 0.5, 'rfg');
  const stone = [[100, 46], [170, 58], [204, 112], [188, 186], [124, 214], [64, 178], [54, 104]];
  s += gem(stone, '#8b93a3', { c: [124, 124], amt: 0.4 });
  const tri = [[128, 82], [170, 158], [86, 158]];
  s += `<polygon points="${P(ngon([128, 130], 56, 6, Math.PI / 6))}" fill="none" stroke="#bff0ff" stroke-width="5" opacity="0.95"/>`;
  s += `<polygon points="${P(tri)}" fill="#5ad1ff" fill-opacity="0.35" stroke="#e6faff" stroke-width="5"/>`;
  s += `<path d="M128 82 L 128 130 M86 158 L 128 130 L 170 158 M128 74 L 79 102 M128 74 L 177 102 M79 102 L 79 158 M177 102 L 177 158" stroke="#bff0ff" stroke-width="3" opacity="0.8"/>`;
  s += `<text x="128" y="146" font-family="Georgia" font-weight="bold" font-size="30" fill="#ffffff" text-anchor="middle">20</text>`;
  s += sparkle([196, 64], 14, '#e6faff') + sparkle([62, 200], 9, '#bff0ff');
  return s;
})()));

icon('ScrollWarding', 'Scroll of Warding', iconFrame('ScrollWarding', BG_ITEM, scroll('ward', '#7a4fd8', '#a77bff')));
icon('ScrollEmbers', 'Scroll of Embers', iconFrame('ScrollEmbers', BG_ITEM, scroll('ember', '#c0392b', '#ff8a2a') +
  sparkle([196, 112], 9, '#ffd45a') + sparkle([210, 146], 6, '#ff8a2a') + sparkle([186, 90], 6, '#ffd45a')));

icon('SharpenedBlade', 'Sharpened Blade', iconFrame('SharpenedBlade', BG_ITEM, (() => {
  let s = glow([150, 100], 100, '#ffffff', 0.25, 'sbl');
  s += sword([70, 200], [196, 56], { blade: '#dfe6ee' });
  s += sparkle([168, 96], 18) + sparkle([140, 128], 11) + sparkle([192, 64], 9, '#fff3c4');
  return s;
})()));

icon('RunicArmor', 'Runic Armor', iconFrame('RunicArmor', BG_ITEM, (() => {
  let s = glow([128, 136], 104, '#5ad1ff', 0.35, 'rag');
  const torso = [[90, 62], [166, 62], [182, 96], [176, 162], [152, 206], [104, 206], [80, 162], [74, 96]];
  s += gem([[52, 70], [94, 60], [100, 102], [62, 112]], STEEL, { w: 8 });
  s += gem([[204, 70], [162, 60], [156, 102], [194, 112]], STEEL, { w: 8 });
  s += gem(torso, '#aab6c3', { c: [128, 118] });
  s += `<path d="M108 62 Q 128 84 148 62" fill="none" stroke="${GOLD}" stroke-width="7" stroke-linecap="round"/>`;
  s += `<path d="M128 84 L 128 196" stroke="${shade('#aab6c3', -0.45)}" stroke-width="3"/>`;
  s += `<path d="M100 102 L 110 118 L 120 102 M110 118 L 110 142 M140 102 L 140 142 M140 104 L 154 116 L 140 128 M108 164 L 118 152 L 128 164 L 138 152 L 148 164" fill="none" stroke="#7fe2ff" stroke-width="5" stroke-linecap="round"/>`;
  s += sparkle([186, 172], 12, '#bff0ff');
  return s;
})()));

icon('ScrapOre', 'Scrap Metal', iconFrame('ScrapOre', BG_ITEM, (() => {
  let s = '';
  // gear
  const g = [];
  for (let i = 0; i < 16; i++) { const a = i * Math.PI / 8, r = i % 2 ? 44 : 56; g.push([156 + Math.cos(a) * r, 98 + Math.sin(a) * r]); }
  s += gem(g, '#9aa3ad', { c: [156, 98] });
  s += `<circle cx="156" cy="98" r="14" fill="#3a3d45" stroke="${INK}" stroke-width="4"/>`;
  s += gem([[50, 150], [110, 120], [140, 160], [96, 206], [56, 196]], '#b07a4a', { amt: 0.4 });
  s += gem([[112, 170], [176, 146], [208, 192], [150, 214]], '#8f98a3', { amt: 0.45 });
  s += gem([[60, 92], [100, 82], [104, 112], [70, 122]], '#c48a52', { w: 6 });
  s += `<circle cx="128" cy="190" r="6" fill="#cfd5dc" stroke="${INK}" stroke-width="3"/><circle cx="80" cy="168" r="5" fill="#e0b080" stroke="${INK}" stroke-width="3"/>`;
  s += sparkle([206, 150], 10, '#ffffff');
  return s;
})()));

icon('SignetRing', 'Signet Ring', iconFrame('SignetRing', BG_ITEM, (() => {
  let s = glow([128, 128], 106, '#ffd66b', 0.4, 'srg');
  const outer = ngon([128, 150], 70, 12, 0, 54), inner = ngon([128, 150], 48, 12, 0, 34);
  s += outline(outer, 9);
  s += facet(outer, GOLD, { c: [128, 150], amt: 0.45 });
  s += poly(inner, '#3b3226');
  s += `<polygon points="${P(inner)}" fill="none" stroke="${INK}" stroke-width="5"/>`;
  s += gem([[96, 98], [160, 98], [172, 118], [128, 136], [84, 118]], '#d9a640', { c: [128, 112], amt: 0.4 });
  s += gem(ngon([128, 86], 26, 8, 0.39), '#d23a3a', { amt: 0.55 });
  s += facet(ngon([122, 80], 9, 6, 0.3), '#ff9a9a', { amt: 0.2 });
  s += sparkle([168, 64], 15) + sparkle([66, 120], 10, '#fff3c4') + sparkle([196, 196], 9, '#fff3c4');
  return s;
})()));

icon('SwampBlossom', 'Swamp Blossom', iconFrame('SwampBlossom', BG_ITEM, (() => {
  let s = '';
  s += `<path d="M128 210 Q 120 170 128 132" stroke="${INK}" stroke-width="12" fill="none" stroke-linecap="round"/><path d="M128 210 Q 120 170 128 132" stroke="#4f9a3a" stroke-width="6" fill="none" stroke-linecap="round"/>`;
  s += gem([[124, 196], [72, 168], [62, 188], [104, 206]], '#4f9a3a', { w: 6 });
  s += gem([[130, 186], [184, 158], [196, 176], [148, 200]], '#5fb044', { w: 6 });
  for (let i = 0; i < 6; i++) {
    const a = -Math.PI / 2 + i * Math.PI / 3, cx = 128 + Math.cos(a) * 36, cy = 104 + Math.sin(a) * 36;
    const petal = ngon([cx, cy], 26, 7, a, 18).map(p => {
      const d = sub(p, [cx, cy]); const ca = Math.cos(a), sa = Math.sin(a);
      return [cx + d[0] * ca - d[1] * sa * 0 , cy + d[1]];
    });
    s += gem(ngon([cx, cy], 25, 7, a), '#5b8cf0', { amt: 0.45, w: 7 });
  }
  s += gem(ngon([128, 104], 18, 8, 0.2), '#ffd45a', { amt: 0.4, w: 6 });
  s += sparkle([186, 64], 12, '#d8e6ff');
  return s;
})()));

icon('GiantElixir', "Giant's Elixir", iconFrame('GiantElixir', BG_ITEM, (() => {
  let s = glow([128, 150], 110, '#ffcf4a', 0.55, 'geg');
  const body = [[112, 64], [144, 64], [146, 104], [192, 196], [178, 214], [78, 214], [64, 196], [110, 104]];
  const liquid = [[96, 146], [160, 146], [186, 196], [174, 208], [82, 208], [70, 196]];
  s += flask(null, body, liquid, '#d9eef5', '#f2a81e', [[106, 40], [150, 40], [148, 66], [108, 66]]);
  s += `<path d="M128 160 l 10 18 l 20 3 l -15 14 l 4 20 l -19 -10 l -19 10 l 4 -20 l -15 -14 l 20 -3 z" fill="#fff3c4" stroke="${INK}" stroke-width="3" transform="translate(0 -6) scale(1)"/>`;
  s += sparkle([186, 82], 14) + sparkle([70, 120], 9, '#fff3c4');
  return s;
})()));

// HUD
icon('Hourglass', 'End Turn (hourglass)', iconFrame('Hourglass', BG_HUD, (() => {
  let s = '';
  const glassTop = [[86, 70], [170, 70], [136, 128], [120, 128]];
  const glassBot = [[120, 128], [136, 128], [170, 186], [86, 186]];
  s += outline(glassTop, 8) + outline(glassBot, 8);
  s += poly(glassTop, '#cfe8f0', 'fill-opacity="0.55"') + poly(glassBot, '#cfe8f0', 'fill-opacity="0.55"');
  s += poly([[100, 84], [156, 84], [134, 118], [122, 118]], '#f0b54a');
  s += poly([[106, 186], [150, 186], [140, 160], [116, 160]], '#f0b54a');
  s += `<line x1="128" y1="122" x2="128" y2="168" stroke="#f0b54a" stroke-width="4"/>`;
  for (const y of [56, 186]) s += gem([[70, y], [186, y], [186, y + 14], [70, y + 14]], WOOD, { w: 7, amt: 0.35 });
  s += gem([[74, 70], [82, 70], [82, 186], [74, 186]], GOLD, { w: 5 }) + gem([[174, 70], [182, 70], [182, 186], [174, 186]], GOLD, { w: 5 });
  s += sparkle([196, 74], 12, '#fff3c4');
  return s;
})()));

icon('Crest_Warrior', 'Crest: Knight', crestFrame('Crest_Warrior', '#8a2018', (() => {
  let s = sword([70, 196], [190, 70], { blade: '#dfe6ee' }) + sword([186, 196], [66, 70], { blade: '#dfe6ee' });
  const sh = [[100, 104], [156, 104], [158, 140], [128, 180], [98, 140]];
  s += gem(sh, GOLD, { c: [128, 132] });
  s += facet(sh.map(p => lerp([128, 132], p, 0.72)), '#b8323a', { c: [128, 132] });
  return s;
})()));
icon('Crest_Mage', 'Crest: Mage', crestFrame('Crest_Mage', '#3a2a8a', (() => {
  let s = glow([128, 92], 70, '#bfe6ff', 0.6, 'cmg');
  s += bevel([96, 206], [150, 92], 7, 7, WOOD);
  s += gem([[150, 64], [170, 86], [158, 112], [138, 96]], '#7fe2ff', { w: 6, amt: 0.5 });
  s += sparkle([118, 84], 26) + sparkle([182, 150], 12, '#e6d8ff') + sparkle([78, 132], 9, '#e6d8ff');
  return s;
})()));
icon('Crest_Rogue', 'Crest: Rogue', crestFrame('Crest_Rogue', '#1d6040', (() => {
  let s = `<path d="M150 50 A 64 64 0 1 0 210 140 A 50 50 0 1 1 150 50 Z" fill="#d8e6d8" opacity="0.35"/>`;
  s += dagger([96, 204], [160, 60], { blade: '#dfe6ee' });
  s += `<path d="M64 122 Q 128 96 192 122 Q 160 140 128 132 Q 96 140 64 122 Z" fill="#1b1f24" stroke="${INK}" stroke-width="5"/><ellipse cx="104" cy="120" rx="10" ry="5" fill="#d8ffc8"/><ellipse cx="152" cy="120" rx="10" ry="5" fill="#d8ffc8"/>`;
  return s;
})()));

// ---------- Round-2 changes from Vili (2026-10-06): 3, 7, 8, 10, 12, 13, 19, 20 ----------
function replaceIcon(name, svg) {
  const i = icons.findIndex(x => x.name === name);
  icons[i].svg = svg;
}

// 3. War Cry: the horn the other way round (bell and sound to the left)
replaceIcon('WarCry', iconFrame('WarCry', BG_WAR, `<g transform="translate(256 0) scale(-1 1)">${(() => {
  const p0 = [50, 200], p1 = [74, 100], p2 = [160, 86];
  const q = t => lerp(lerp(p0, p1, t), lerp(p1, p2, t), t);
  const N = 9; let L = [], R = [];
  for (let i = 0; i <= N; i++) {
    const t = i / N, p = q(t), d = norm(sub(q(Math.min(1, t + 0.01)), q(Math.max(0, t - 0.01))));
    const v = [-d[1], d[0]], w = 6 + 34 * t * t;
    L.push(add(p, mul(v, -w))); R.push(add(p, mul(v, w)));
  }
  let s = glow([170, 88], 70, '#ffe08a', 0.35, 'wcg2');
  s += outline(L.concat(R.slice().reverse()), 9);
  for (let i = 0; i < N; i++) {
    const t = (i + 0.5) / N, band = (t > 0.32 && t < 0.42) || (t > 0.7 && t < 0.8);
    const base = band ? GOLD : '#efe2c2';
    s += poly([L[i], L[i + 1], q((i + 1) / N), q(i / N)], shade(base, 0.25));
    s += poly([q(i / N), q((i + 1) / N), R[i + 1], R[i]], shade(base, -0.25));
  }
  const bellC = q(1);
  s += `<ellipse cx="${bellC[0]}" cy="${bellC[1]}" rx="9" ry="40" fill="#3a2414" stroke="${INK}" stroke-width="5" transform="rotate(-8 ${bellC[0]} ${bellC[1]})"/>`;
  for (let k = 0; k < 3; k++) {
    const r = 20 + k * 15;
    s += `<path d="M${178 + k * 5} ${88 - r} A ${r} ${r} 0 0 1 ${178 + k * 5} ${88 + r}" fill="none" stroke="#fff3c4" stroke-opacity="${0.95 - k * 0.25}" stroke-width="${7 - k}" stroke-linecap="round"/>`;
  }
  return s;
})()}</g>`));

// 7. Fireball: one big flame
replaceIcon('Fireball', iconFrame('Fireball', BG_MAGE, (() => {
  let s = glow([128, 140], 120, '#ff8a2a', 0.7, 'fig2');
  const c = [128, 168];
  const outer = [[128, 26], [146, 76], [178, 52], [176, 102], [206, 92], [198, 150], [186, 192], [158, 216], [98, 216], [70, 192], [56, 150], [52, 100], [84, 116], [80, 66], [110, 86]];
  s += gem(outer, '#e8441c', { c, amt: 0.4 });
  s += facet(outer.map(p => lerp(c, p, 0.74)), '#ff8a2a', { c, amt: 0.4 });
  s += facet(outer.map(p => lerp([128, 178], p, 0.47)), '#ffd45a', { c: [128, 178], amt: 0.35 });
  s += facet(ngon([128, 182], 16, 7, 0.3, 22), '#fff6c8', { amt: 0.15 });
  s += sparkle([200, 52], 12, '#fff3c4') + sparkle([54, 66], 9, '#ffd45a') + sparkle([212, 196], 7, '#ff8a2a');
  return s;
})()));

// 8. Mana Shield: a light-blue, see-through bubble
replaceIcon('ManaShield', iconFrame('ManaShield', BG_MAGE, (() => {
  let s = glow([128, 128], 118, '#8fe3ff', 0.55, 'msg2');
  s += `<defs><radialGradient id="bub" cx="38%" cy="32%" r="70%"><stop offset="0" stop-color="#f2fcff" stop-opacity="0.75"/><stop offset="0.55" stop-color="#9fe0ff" stop-opacity="0.38"/><stop offset="1" stop-color="#5ab8ff" stop-opacity="0.6"/></radialGradient>
    <clipPath id="bubclip"><circle cx="128" cy="128" r="84"/></clipPath></defs>`;
  s += `<circle cx="128" cy="128" r="88" fill="none" stroke="${INK}" stroke-opacity="0.7" stroke-width="7"/>`;
  s += `<circle cx="128" cy="128" r="84" fill="url(#bub)"/>`;
  let lattice = '';
  for (let row = -3; row <= 3; row++) for (let col = -3; col <= 3; col++) {
    const x = 128 + col * 36 + (row % 2 ? 18 : 0), y = 128 + row * 31;
    lattice += `<polygon points="${P(ngon([x, y], 20, 6, Math.PI / 6))}" fill="none" stroke="#e6faff" stroke-width="2"/>`;
  }
  s += `<g clip-path="url(#bubclip)" opacity="0.35">${lattice}</g>`;
  s += `<circle cx="128" cy="128" r="84" fill="none" stroke="#c8f2ff" stroke-width="4"/>`;
  s += `<path d="M70 104 A 64 64 0 0 1 120 60" fill="none" stroke="#ffffff" stroke-opacity="0.9" stroke-width="9" stroke-linecap="round"/>`;
  s += `<ellipse cx="96" cy="86" rx="10" ry="6" fill="#ffffff" opacity="0.9" transform="rotate(-35 96 86)"/>`;
  s += `<path d="M84 196 A 84 84 0 0 0 196 168" fill="none" stroke="#e6faff" stroke-opacity="0.6" stroke-width="5" stroke-linecap="round"/>`;
  s += sparkle([196, 62], 13, '#e6faff');
  return s;
})()));

// 10. Arcane Chains: more chains, crossing over a glowing seal
function chainStrand(a, b, count) {
  const dir = norm(sub(b, a)), ang = Math.atan2(dir[1], dir[0]) * 180 / Math.PI;
  let s = '';
  for (let i = 0; i < count; i++) {
    const [x, y] = lerp(a, b, (i + 0.5) / count);
    if (i % 2 === 0) {
      s += `<g transform="rotate(${ang} ${x} ${y})">
        <rect x="${x - 23}" y="${y - 14}" width="46" height="28" rx="14" fill="none" stroke="${INK}" stroke-width="17"/>
        <rect x="${x - 23}" y="${y - 14}" width="46" height="28" rx="14" fill="none" stroke="#9a72f0" stroke-width="10"/>
        <path d="M${x - 16} ${y - 14} L ${x + 10} ${y - 14}" stroke="#e6d8ff" stroke-width="3" stroke-linecap="round"/></g>`;
    } else {
      s += `<g transform="rotate(${ang} ${x} ${y})">
        <rect x="${x - 24}" y="${y - 6}" width="48" height="12" rx="6" fill="#7a52d0" stroke="${INK}" stroke-width="5"/>
        <path d="M${x - 16} ${y - 2} L ${x + 14} ${y - 2}" stroke="#d8c6ff" stroke-width="3" stroke-linecap="round"/></g>`;
    }
  }
  return s;
}
replaceIcon('ArcaneChains', iconFrame('ArcaneChains', BG_MAGE, (() => {
  let s = glow([128, 128], 116, '#a77bff', 0.55, 'acg2');
  s += chainStrand([30, 180], [226, 196], 7);
  s += chainStrand([40, 40], [216, 216], 8);
  s += chainStrand([216, 40], [40, 216], 8);
  s += glow([128, 128], 48, '#e6d8ff', 0.9, 'acs');
  s += gem(ngon([128, 128], 24, 8, 0.39), '#b28cff', { amt: 0.45, w: 6 });
  s += `<path d="M120 116 L 128 128 L 136 116 M128 128 L 128 142" stroke="#ffffff" stroke-width="4" stroke-linecap="round" fill="none"/>`;
  s += sparkle([206, 58], 12, '#f4eeff') + sparkle([56, 70], 9, '#d8c6ff');
  return s;
})()));

// 12. Backstab: real slash marks
replaceIcon('Backstab', iconFrame('Backstab', BG_ROGUE, (() => {
  let s = `<path d="M150 40 A 70 70 0 1 0 216 150 A 56 56 0 1 1 150 40 Z" fill="#cfd8e6" opacity="0.25"/>`;
  s += glow([108, 150], 80, '#ff3a2a', 0.45, 'bsg');
  for (const [x1, y1, x2, y2, bend] of [[44, 120, 118, 96, 18], [52, 162, 140, 132, 22], [74, 204, 160, 170, 20]]) {
    const mx = (x1 + x2) / 2, my = (y1 + y2) / 2 - bend, mx2 = (x1 + x2) / 2, my2 = (y1 + y2) / 2 - bend + 12;
    s += `<path d="M${x1} ${y1} Q ${mx} ${my} ${x2} ${y2} Q ${mx2} ${my2} ${x1} ${y1} Z" fill="${INK}" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>`;
    s += `<path d="M${x1} ${y1} Q ${mx} ${my} ${x2} ${y2} Q ${mx2} ${my2} ${x1} ${y1} Z" fill="#e8302a"/>`;
    s += `<path d="M${x1 + 10} ${y1 - 3} Q ${mx} ${my + 4} ${x2 - 8} ${y2 - 1}" fill="none" stroke="#ffb0a0" stroke-width="2.5" stroke-linecap="round"/>`;
  }
  s += dagger([196, 50], [92, 182], { blade: '#d8dee6' });
  s += sparkle([92, 186], 16, '#ffd0c8');
  return s;
})()));

// 13. Smoke Bomb: a big smoke cloud with the bomb in front
replaceIcon('SmokeBomb', iconFrame('SmokeBomb', BG_ROGUE, (() => {
  let s = '';
  const back = [[70, 86, 44], [128, 64, 48], [186, 84, 44], [204, 140, 40], [52, 140, 36], [150, 118, 52], [96, 120, 50]];
  for (const [x, y, r] of back) s += `<g opacity="0.85">${puff([x, y], r, '#8c94a3')}</g>`;
  const front = [[164, 178, 40], [210, 196, 26], [128, 196, 30]];
  for (const [x, y, r] of front) s += puff([x, y], r, '#a9b0bc');
  const c = [88, 178];
  s += gem(ngon(c, 40, 10, 0.1), '#33363f', { amt: 0.5 });
  s += facet(ngon([78, 166], 12, 6, 0.3), '#7a8094', { amt: 0.3 });
  s += gem([[104, 136], [122, 142], [117, 156], [99, 150]], '#8a8f9c', { w: 5 });
  s += `<path d="M114 140 Q 128 124 120 108" stroke="${INK}" stroke-width="8" fill="none" stroke-linecap="round"/><path d="M114 140 Q 128 124 120 108" stroke="#c9a46a" stroke-width="4" fill="none" stroke-linecap="round"/>`;
  s += glow([120, 104], 26, '#ffd45a', 0.85, 'sbg2') + sparkle([120, 104], 14, '#fff3c4');
  return s;
})()));

// 19, 20. Scrolls with a much bigger seal
function bigScroll(seal, sealColor, accent) {
  let s = '';
  const sheet = [[74, 64], [182, 64], [178, 194], [78, 194]];
  s += outline(sheet, 8);
  s += poly(sheet, '#ead9a8');
  s += poly([[74, 64], [128, 64], [128, 194], [78, 194]], '#f3e6bd');
  for (let y = 88; y <= 120; y += 16) s += `<line x1="92" y1="${y}" x2="164" y2="${y}" stroke="#b49a63" stroke-width="4" stroke-linecap="round"/>`;
  for (const y of [60, 198]) {
    const roll = [[62, y - 11], [194, y - 11], [194, y + 11], [62, y + 11]];
    s += outline(roll, 8);
    s += poly([[62, y - 11], [194, y - 11], [194, y], [62, y]], '#f6ebc8');
    s += poly([[62, y], [194, y], [194, y + 11], [62, y + 11]], '#c9b07a');
    s += gem(ngon([62, y], 11, 6), '#a8875a', { w: 6 });
    s += gem(ngon([194, y], 11, 6), '#a8875a', { w: 6 });
  }
  const c = [140, 160];
  s += glow(c, 80, accent, 0.65, 'bsg' + seal);
  s += gem(ngon(c, 50, 12, 0.1), sealColor, { amt: 0.45, w: 9 });
  s += `<circle cx="${c[0]}" cy="${c[1]}" r="38" fill="none" stroke="${shade(sealColor, 0.35)}" stroke-width="3" opacity="0.8"/>`;
  if (seal === 'ward') {
    s += `<polygon points="${P(ngon(c, 24, 6, Math.PI / 6))}" fill="none" stroke="#f1e2ff" stroke-width="6"/><circle cx="${c[0]}" cy="${c[1]}" r="7" fill="#f1e2ff"/>`;
  } else {
    s += `<path d="M140 128 C 158 148 162 162 152 180 C 146 190 134 190 128 180 C 120 166 134 158 140 128 Z" fill="#ffd36a" stroke="#fff3c4" stroke-width="3"/>`;
    s += `<path d="M140 156 C 147 164 148 172 143 178 C 140 182 136 182 134 178 C 131 172 137 168 140 156 Z" fill="#fff6c8"/>`;
  }
  return s;
}
replaceIcon('ScrollWarding', iconFrame('ScrollWarding', BG_ITEM, bigScroll('ward', '#7a4fd8', '#a77bff') + sparkle([200, 112], 10, '#e6d8ff')));
replaceIcon('ScrollEmbers', iconFrame('ScrollEmbers', BG_ITEM, bigScroll('ember', '#c0392b', '#ff8a2a') +
  sparkle([200, 112], 10, '#ffd45a') + sparkle([212, 150], 7, '#ff8a2a') + sparkle([190, 92], 6, '#ffd45a')));

// ---------- write the sheet ----------
const COLS = 6, CELL = 256;
const rows = Math.ceil(icons.length / COLS);
let html = `<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;background:transparent}.c{position:absolute;width:256px;height:256px}</style></head><body>`;
icons.forEach((ic, i) => {
  html += `<div class="c" style="left:${(i % COLS) * CELL}px;top:${Math.floor(i / COLS) * CELL}px">${ic.svg}</div>`;
});
html += `</body></html>`;
fs.writeFileSync('sheet.html', html);
fs.writeFileSync('order.json', JSON.stringify({ cols: COLS, rows, icons: icons.map(i => ({ name: i.name, label: i.label })) }, null, 1));
console.log(`${icons.length} icons, sheet ${COLS * CELL}x${rows * CELL}`);
