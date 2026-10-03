"""Render the zone 2-7 floor plans (SVG) and write the build specs (Markdown + JSON) from zone_data.py.

python3 zone_build.py <out_dir>
  <out_dir>/plans_svg/*.svg   floor plans (render to PNG with render_plans.js)
  <out_dir>/*.md, zone-plans.json
"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zone_data import ZONES, NYK, UUSI, KORJ, PAIK

W, H, PANEL = 1600, 1000, 400
BG, PAPER, LINE, FAINT = '#13263f', '#173052', '#e8f1ff', '#2b4a74'
CYAN, ORANGE, RED, GOLD, GREEN, PURPLE, YEL, FIX = '#5fe1ff', '#ffab4a', '#ff5d5d', '#ffd24a', '#8ff0a0', '#c89bff', '#ffe98a', '#ff7a45'
FONT = 'DejaVu Sans, sans-serif'


def esc(t):
    return str(t).replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')


class Plan:
    def __init__(s, zone):
        sx, sz = zone['extent']; cx, cz = zone['center']
        area_w, area_h = W - PANEL - 300, H - 230
        m = max(sx, sz) * 0.12          # room outside the walls for door arrows and labels
        s.k = min(area_w / (sx + m), area_h / (sz + m))
        s.ox = 170 + area_w / 2 - cx * s.k
        s.oz = 125 + area_h / 2 + cz * s.k
        s.sx, s.sz, s.cx, s.cz, s.step = sx, sz, cx, cz, zone['step']
        s.o = []

    def P(s, x, z):
        return (s.ox + x * s.k, s.oz - z * s.k)

    def add(s, t):
        s.o.append(t)

    # ---------------- primitives
    def rect(s, x, z, w, d, fill='none', stroke=LINE, sw=2.0, dash=None, op=1, fop=None, rot=0):
        px, py = s.P(x, z)
        da = f' stroke-dasharray="{dash}"' if dash else ''
        fo = f' fill-opacity="{fop}"' if fop is not None else ''
        tr = f' transform="rotate({rot} {px:.1f} {py:.1f})"' if rot else ''   # Unity +yaw = clockwise from above = SVG +rotate
        s.add(f'<rect x="{px - w * s.k / 2:.1f}" y="{py - d * s.k / 2:.1f}" width="{w * s.k:.1f}" height="{d * s.k:.1f}" '
              f'fill="{fill}"{fo} stroke="{stroke}" stroke-width="{sw}"{da} opacity="{op}"{tr}/>')

    def circle(s, x, z, r, col, fop=0.16, dash='6 4', sw=1.6, fill=None):
        px, py = s.P(x, z)
        da = f' stroke-dasharray="{dash}"' if dash else ''
        s.add(f'<circle cx="{px:.1f}" cy="{py:.1f}" r="{r * s.k:.1f}" fill="{fill or col}" fill-opacity="{fop}" '
              f'stroke="{col}" stroke-width="{sw}"{da}/>')

    def text(s, x, z, t, col=LINE, size=12, anchor='middle', bold=False, dy=4):
        px, py = s.P(x, z)
        b = ' font-weight="bold"' if bold else ''
        s.add(f'<text x="{px:.1f}" y="{py + dy:.1f}" fill="{col}" font-family="{FONT}" font-size="{size}" '
              f'text-anchor="{anchor}"{b} paint-order="stroke" stroke="{PAPER}" stroke-width="3">{esc(t)}</text>')

    def side_text(s, x, z, t, side):
        px, py = s.P(x, z)
        if side == 'r': args = (px + 18, py + 4, 'start')
        elif side == 'l': args = (px - 18, py + 4, 'end')
        elif side == 'b': args = (px, py + 28, 'middle')
        else: args = (px, py - 20, 'middle')
        s.add(f'<text x="{args[0]:.1f}" y="{args[1]:.1f}" fill="{LINE}" font-family="{FONT}" font-size="13" '
              f'text-anchor="{args[2]}" paint-order="stroke" stroke="{PAPER}" stroke-width="3">{esc(t)}</text>')

    def polyline(s, pts, col, width_px, op=1, dash=None, cap='round'):
        d = 'M' + ' L'.join(f'{s.P(x, z)[0]:.1f},{s.P(x, z)[1]:.1f}' for x, z in pts)
        da = f' stroke-dasharray="{dash}"' if dash else ''
        s.add(f'<path d="{d}" fill="none" stroke="{col}" stroke-width="{width_px:.1f}" stroke-linejoin="round" '
              f'stroke-linecap="{cap}" opacity="{op}"{da}/>')

    def polygon(s, pts, col, fop=0.16, dash='6 4'):
        p = ' '.join(f'{s.P(x, z)[0]:.1f},{s.P(x, z)[1]:.1f}' for x, z in pts)
        s.add(f'<polygon points="{p}" fill="{col}" fill-opacity="{fop}" stroke="{col}" stroke-width="1.6" stroke-dasharray="{dash}"/>')

    def hexagon(s, x, z, r, col, dash=None, fop=0.22):
        px, py = s.P(x, z)
        da = f' stroke-dasharray="{dash}"' if dash else ''
        pts = ' '.join(f'{px + r * s.k * math.cos(math.pi / 3 * i):.1f},{py + r * s.k * math.sin(math.pi / 3 * i):.1f}' for i in range(6))
        s.add(f'<polygon points="{pts}" fill="{col}" fill-opacity="{fop}" stroke="{col}" stroke-width="1.4"{da}/>')

    # ---------------- markers
    def marker(s, x, z, kind, ring=None):
        px, py = s.P(x, z)
        if kind == 'boss':
            s.add(f'<polygon points="{px},{py - 14} {px + 14},{py} {px},{py + 14} {px - 14},{py}" fill="{RED}" stroke="#fff" stroke-width="1.5"/>')
        elif kind == 'enemy':
            s.add(f'<circle cx="{px}" cy="{py}" r="9" fill="{RED}" stroke="#fff" stroke-width="1.5"/>')
        elif kind == 'npc':
            s.add(f'<circle cx="{px}" cy="{py}" r="9" fill="{YEL}" stroke="#fff" stroke-width="1.5"/>')
        elif kind == 'chest':
            s.add(f'<rect x="{px - 10}" y="{py - 8}" width="20" height="16" fill="{GOLD}" stroke="#fff" stroke-width="1.5"/>')
        elif kind == 'pickup':
            s.add(f'<polygon points="{px},{py - 8} {px + 7},{py} {px},{py + 8} {px - 7},{py}" fill="{GREEN}" stroke="#fff" stroke-width="1"/>')
        elif kind == 'shrine':
            s.add(f'<polygon points="{px},{py - 16} {px + 12},{py} {px},{py + 16} {px - 12},{py}" fill="{CYAN}" stroke="#fff" stroke-width="1.5"/>')
        elif kind == 'light':
            s.add(f'<circle cx="{px}" cy="{py}" r="6" fill="{ORANGE}"/><circle cx="{px}" cy="{py}" r="12" fill="none" stroke="{ORANGE}" stroke-width="1"/>')
        if ring:
            s.add(f'<circle cx="{px}" cy="{py}" r="17" fill="none" stroke="{ring}" stroke-width="1.6" stroke-dasharray="4 3"/>')

    def spawn(s, x, z, yaw):
        px, py = s.P(x, z)
        a = math.radians(yaw)
        dx, dy = math.sin(a), -math.cos(a)
        tip = (px + dx * 18, py + dy * 18)
        l = (px - dy * 8 - dx * 4, py + dx * 8 - dy * 4)
        r = (px + dy * 8 - dx * 4, py - dx * 8 - dy * 4)
        s.add(f'<circle cx="{px}" cy="{py}" r="8" fill="none" stroke="{CYAN}" stroke-width="2"/>')
        s.add(f'<polygon points="{tip[0]:.1f},{tip[1]:.1f} {l[0]:.1f},{l[1]:.1f} {r[0]:.1f},{r[1]:.1f}" fill="{CYAN}"/>')

    def door(s, e):
        x, _, z = e['pos']; gap = e['gap']; out = e['outward']
        if e['axis'] == 'x':
            s.rect(x, z, gap, 2.2, fill=PAPER, stroke=PAPER, sw=0)
            a, b = s.P(x - gap / 2, z), s.P(x + gap / 2, z)
            s.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="{CYAN}" stroke-width="4"/>')
            p0, p1 = s.P(x, z), s.P(x, z + out * 4)
            ly = p1[1] + (-10 if out > 0 else 18)
            s.add(f'<line x1="{p0[0]:.1f}" y1="{p0[1]:.1f}" x2="{p1[0]:.1f}" y2="{p1[1]:.1f}" stroke="{CYAN}" stroke-width="2.5" marker-end="url(#arr)"/>')
            s.add(f'<text x="{p1[0]:.1f}" y="{ly:.1f}" fill="{CYAN}" font-family="{FONT}" font-size="14" font-weight="bold" text-anchor="middle" paint-order="stroke" stroke="{BG}" stroke-width="4">{esc(e["label"])}</text>')
        else:
            s.rect(x, z, 2.2, gap, fill=PAPER, stroke=PAPER, sw=0)
            a, b = s.P(x, z - gap / 2), s.P(x, z + gap / 2)
            s.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="{CYAN}" stroke-width="4"/>')
            p0, p1 = s.P(x, z), s.P(x + out * 4, z)
            anc = 'start' if out > 0 else 'end'
            s.add(f'<line x1="{p0[0]:.1f}" y1="{p0[1]:.1f}" x2="{p1[0]:.1f}" y2="{p1[1]:.1f}" stroke="{CYAN}" stroke-width="2.5" marker-end="url(#arr)"/>')
            s.add(f'<text x="{p1[0] + out * 8:.1f}" y="{p1[1] - 12:.1f}" fill="{CYAN}" font-family="{FONT}" font-size="14" font-weight="bold" text-anchor="{anc}" paint-order="stroke" stroke="{BG}" stroke-width="4">{esc(e["label"])}</text>')

    def grid(s, x, z, tiles, tile):
        span = tiles * tile
        x0, z0 = x - span / 2, z - span / 2
        for i in range(tiles + 1):
            w = 1.6 if i in (0, tiles) else 0.6
            a, b = s.P(x0 + i * tile, z0), s.P(x0 + i * tile, z0 + span)
            s.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="{ORANGE}" stroke-width="{w}" opacity="0.85"/>')
            a, b = s.P(x0, z0 + i * tile), s.P(x0 + span, z0 + i * tile)
            s.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="{ORANGE}" stroke-width="{w}" opacity="0.85"/>')
        s.rect(x, z, span, span, fill=ORANGE, stroke='none', sw=0, fop=0.07)
        s.text(x0 + 0.3, z0 + span + 1.4, f'Ruudukko {tiles}×{tiles} (GitHub master)', ORANGE, 12, anchor='start')

    def frame(s):
        s.rect(s.cx, s.cz, s.sx, s.sz, fill=PAPER, stroke='none', sw=0)
        step = s.step
        x = math.ceil((s.cx - s.sx / 2) / step) * step
        while x <= s.cx + s.sx / 2 + 1e-6:
            a, b = s.P(x, s.cz - s.sz / 2), s.P(x, s.cz + s.sz / 2)
            s.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="{FAINT}" stroke-width="{1.2 if abs(x % (step * 2)) < 1e-6 else 0.6}"/>')
            x += step
        z = math.ceil((s.cz - s.sz / 2) / step) * step
        while z <= s.cz + s.sz / 2 + 1e-6:
            a, b = s.P(s.cx - s.sx / 2, z), s.P(s.cx + s.sx / 2, z)
            s.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="{FAINT}" stroke-width="{1.2 if abs(z % (step * 2)) < 1e-6 else 0.6}"/>')
            z += step
        o = s.P(0, 0)
        s.add(f'<circle cx="{o[0]:.1f}" cy="{o[1]:.1f}" r="4" fill="none" stroke="{FAINT}" stroke-width="2"/>')
        s.text(0, -1.6 * (s.step / 5 if s.step < 5 else 1), '(0,0)', '#6f8fbf', 11)


ORDER = ['path', 'region', 'crack', 'prop', 'tower', 'circle', 'rock', 'brazier', 'torch', 'dais', 'wall', 'gate', 'pillar', 'tree',
         'window', 'banner', 'barrier', 'trigger', 'grid', 'door', 'light', 'pickup', 'chest', 'pedestal', 'shrine', 'npc', 'enemy', 'boss',
         'spawn', 'text']


def status_col(e, default=GREEN):
    return e.get('color') or default


def draw_zone(z):
    s = Plan(z)
    s.frame()
    els = [e for e in z['elements'] if e.get('draw', True) and e['kind'] not in ('floor', 'scatter', 'fx')]
    els.sort(key=lambda e: ORDER.index(e['kind']) if e['kind'] in ORDER else 99)
    labels = []   # draw labels last so nothing covers them
    for e in els:
        k, st = e['kind'], e['status']
        x, y, zz = e['pos']
        new = st == UUSI
        if k == 'path':
            s.polyline(e['pts'], '#c89a5c', e['width'] * s.k, op=0.4, dash=None if e['width'] > 4 else '10 6')
        elif k == 'region':
            s.polygon(e['pts'], status_col(e, GOLD))
        elif k == 'crack':
            for pl in e['polylines']:
                s.polyline(pl, '#ff6a1a', 3, cap='round')
        elif k in ('prop', 'tower', 'rock', 'brazier', 'torch'):
            w, _, d = e['size']
            col = status_col(e, '#b9c3cf' if k == 'rock' else (ORANGE if k in ('brazier', 'torch') else GREEN))
            if k in ('brazier', 'torch'):
                s.rect(x, zz, max(w, 1.2), max(d, 1.2), fill=col, stroke=col, sw=1.6, dash='4 3', fop=0.35, rot=e['rot'])
                s.marker(x, zz, 'light')
            else:
                s.rect(x, zz, w, d, fill=col, stroke=col, sw=1.6, dash='6 4', fop=0.16, rot=e['rot'])
        elif k == 'circle':
            col = status_col(e)
            s.circle(x, zz, e['r'], col, dash='6 4' if new else None)
        elif k == 'dais':
            w, _, d = e['size']
            s.rect(x, zz, w, d, fill='#2d4a70', stroke=LINE, sw=2)
        elif k == 'wall':
            w, _, d = e['size']
            if new:
                s.rect(x, zz, w, d, fill=GREEN, stroke=GREEN, sw=1.6, dash='6 4', fop=0.16, rot=e['rot'])
            else:
                s.rect(x, zz, w, d, fill=LINE, stroke=LINE, sw=1, rot=e['rot'])
                if st == KORJ:
                    s.rect(x, zz, w + 0.8, d + 0.8, stroke=FIX, sw=2.2, dash='5 3', rot=e['rot'])
        elif k == 'gate':
            w, _, d = e['size']
            s.rect(x, zz, w, d, fill=LINE, stroke=GOLD, sw=2)
        elif k == 'pillar':
            if new:
                s.circle(x, zz, e['r'], GREEN, fop=0.3, dash=None)
            else:
                s.circle(x, zz, e['r'], LINE, fop=0.9, dash=None, sw=1)
        elif k == 'tree':
            if new:
                s.hexagon(x, zz, e['r'], GREEN, dash='5 3')
            else:
                s.hexagon(x, zz, e['r'], LINE, fop=0.15)
        elif k in ('window', 'banner'):
            w, _, d = e['size']
            col = status_col(e, PURPLE)
            s.rect(x, zz, max(w, 0.6), max(d, 0.6), fill=col, stroke=col, sw=1.4, dash='4 3', fop=0.35, rot=e['rot'])
        elif k == 'barrier':
            w, _, d = e['size']
            s.rect(x, zz, w, d, fill=ORANGE, stroke=ORANGE, sw=1, op=0.6)
        elif k == 'trigger':
            w, _, d = e['size']
            if st == PAIK:
                s.rect(x, zz, w, d, stroke=ORANGE, sw=2.2, dash='10 5')
            else:
                s.rect(x, zz, w, d, stroke=ORANGE, sw=1.4, dash='3 5')
            if e.get('label'):
                labels.append(('text', x + w / 2 - 1, zz - d / 2 + 1.4, e['label'], ORANGE, 11, 'end'))
        elif k == 'grid':
            s.grid(x, zz, e['tiles'], e['tile'])
        elif k == 'door':
            s.door(e)
        elif k == 'light':
            s.marker(x, zz, 'light', ring=GREEN if new else None)
        elif k in ('pickup', 'chest', 'shrine', 'npc', 'enemy', 'boss'):
            s.marker(x, zz, k, ring=(YEL if st == PAIK else (GREEN if new else None)))
        elif k == 'pedestal':
            w, _, d = e['size']
            if e.get('was_pos'):
                ox, _, oz = e['was_pos']
                s.rect(ox, oz, w, d, stroke=FIX, sw=1.6, dash='3 3')
                a, b = s.P(ox, oz), s.P(x, zz)
                s.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="{FIX}" stroke-width="1.6" stroke-dasharray="4 3" marker-end="url(#arrfix)"/>')
            s.rect(x, zz, w, d, fill=LINE, stroke=FIX if st == KORJ else LINE, sw=2)
        elif k == 'spawn':
            s.spawn(x, zz, e['rot'])
        elif k == 'text':
            labels.append(('text', x, zz, e['label'], e.get('color', LINE), 12, e.get('anchor', 'middle')))
            continue
        # labels
        lab = e.get('label')
        if not lab or k in ('door', 'trigger'):
            continue
        if k in ('pickup', 'chest', 'shrine', 'npc', 'enemy', 'boss', 'spawn', 'light', 'pedestal'):
            labels.append(('side', x, zz, lab, e.get('side', 'r')))
        elif k == 'spawn':
            pass
        else:
            lx, lz, anc = e.get('lab', (x, zz, 'middle'))
            col = GOLD if k == 'gate' else (LINE if k in ('wall', 'dais') and st != UUSI else status_col(e, ORANGE if k in ('brazier', 'torch') else ('#e0b880' if k == 'path' else ('#b9c3cf' if k == 'rock' else GREEN))))
            if k == 'gate':
                lx, lz, anc = x + 1, zz + 6, 'middle'
            labels.append(('text', lx, lz, lab, col, 13 if k == 'gate' else 12, anc))
    # spawn labels
    for e in els:
        if e['kind'] == 'spawn':
            labels.append(('side', e['pos'][0], e['pos'][2], e['id'], e.get('side', 'r')))
    for L in labels:
        if L[0] == 'side':
            s.side_text(L[1], L[2], L[3], L[4])
        else:
            s.text(L[1], L[2], L[3], L[4], L[5], anchor=L[6], bold=(L[5] == 13))
    return s


def legend_and_frame(s, z):
    kinds = {e['kind'] for e in z['elements']}
    statuses = {e['status'] for e in z['elements']}
    items = [('wall', 'Seinä (nykyinen)'), ('new', 'Uusi (konseptikuvista)')]
    if KORJ in statuses: items.append(('fix', 'Korjaus (muutettu arvo)'))
    if PAIK in statuses: items.append(('local', 'Paikallisen masterin arvo'))
    items += [('door', 'Ovi toiseen alueeseen'), ('spawn', 'Spawn-piste + katsesuunta')]
    if 'grid' in kinds: items += [('grid', 'Taisteluruudukko'), ('trigger', 'Taistelun laukaisualue')]
    if 'boss' in kinds: items.append(('boss', 'Pomo'))
    if 'enemy' in kinds: items.append(('enemy', 'Vihollinen'))
    if 'npc' in kinds: items.append(('npc', 'Dialogi-NPC'))
    if 'chest' in kinds: items.append(('chest', 'Palkintoarkku'))
    if 'shrine' in kinds: items.append(('shrine', 'Ruunikivialttari'))
    if 'tree' in kinds: items.append(('tree', 'Puu (valkoinen = nykyinen)'))
    if 'pickup' in kinds: items.append(('pickup', 'Keräiltävä'))
    if 'light' in kinds: items.append(('light', 'Valo (vihreä rengas = uusi)'))
    head = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">'
            f'<defs><marker id="arr" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">'
            f'<path d="M0,0 L10,5 L0,10 z" fill="{CYAN}"/></marker>'
            f'<marker id="arrfix" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">'
            f'<path d="M0,0 L10,5 L0,10 z" fill="{FIX}"/></marker></defs>'
            f'<rect width="{W}" height="{H}" fill="{BG}"/>')
    t = [f'<text x="60" y="58" fill="{GOLD}" font-family="DejaVu Serif, serif" font-size="30" font-weight="bold">Zone {z["num"]} · {esc(z["fi"])}</text>',
         f'<text x="60" y="86" fill="#a9bfe0" font-family="{FONT}" font-size="15">{esc(z["subtitle"])}</text>']
    x0, y0 = 60, H - 36
    L = 10 if s.k * 10 < 320 else 5
    t.append(f'<rect x="{x0}" y="{y0}" width="{L * s.k:.1f}" height="8" fill="{LINE}"/><rect x="{x0 + L / 2 * s.k:.1f}" y="{y0}" width="{L / 2 * s.k:.1f}" height="8" fill="{BG}" stroke="{LINE}" stroke-width="1"/>')
    t.append(f'<text x="{x0}" y="{y0 - 8}" fill="{LINE}" font-family="{FONT}" font-size="12">0</text><text x="{x0 + L * s.k:.1f}" y="{y0 - 8}" fill="{LINE}" font-family="{FONT}" font-size="12" text-anchor="middle">{L} m</text>')
    t.append(f'<text x="{x0 + L * s.k + 30:.1f}" y="{y0 + 8}" fill="#a9bfe0" font-family="{FONT}" font-size="12">Ruudukko {s.step:g} m · X oikealle, Z ylös (pohjoinen) · Unityn maailmakoordinaatit (m)</text>')
    nx, ny = W - PANEL - 50, 60
    t.append(f'<polygon points="{nx},{ny - 22} {nx + 10},{ny + 8} {nx},{ny} {nx - 10},{ny + 8}" fill="{LINE}"/><text x="{nx}" y="{ny + 28}" fill="{LINE}" font-family="{FONT}" font-size="14" text-anchor="middle" font-weight="bold">N (+Z)</text>')
    lx = W - PANEL + 10
    t.append(f'<rect x="{lx - 10}" y="0" width="{PANEL}" height="{H}" fill="#0f1f35"/>')
    t.append(f'<text x="{lx + 16}" y="46" fill="{GOLD}" font-family="{FONT}" font-size="15" font-weight="bold" letter-spacing="2">SELITTEET</text>')
    y = 78
    for kind, lab in items:
        cx, cy = lx + 30, y
        if kind == 'wall': t.append(f'<rect x="{cx - 14}" y="{cy - 5}" width="28" height="10" fill="{LINE}"/>')
        elif kind == 'new': t.append(f'<rect x="{cx - 14}" y="{cy - 8}" width="28" height="16" fill="{GREEN}" fill-opacity="0.16" stroke="{GREEN}" stroke-dasharray="5 3" stroke-width="1.6"/>')
        elif kind == 'fix': t.append(f'<rect x="{cx - 14}" y="{cy - 6}" width="28" height="12" fill="{LINE}" stroke="{FIX}" stroke-width="2.2" stroke-dasharray="5 3"/>')
        elif kind == 'local': t.append(f'<rect x="{cx - 14}" y="{cy - 8}" width="28" height="16" fill="none" stroke="{ORANGE}" stroke-width="2.2" stroke-dasharray="8 4"/><circle cx="{cx}" cy="{cy}" r="4" fill="{YEL}"/>')
        elif kind == 'door': t.append(f'<line x1="{cx - 14}" y1="{cy}" x2="{cx + 14}" y2="{cy}" stroke="{CYAN}" stroke-width="4"/>')
        elif kind == 'spawn': t.append(f'<circle cx="{cx}" cy="{cy}" r="8" fill="none" stroke="{CYAN}" stroke-width="2"/><polygon points="{cx},{cy - 16} {cx - 6},{cy - 6} {cx + 6},{cy - 6}" fill="{CYAN}"/>')
        elif kind == 'grid': t.append(f'<rect x="{cx - 12}" y="{cy - 10}" width="24" height="20" fill="{ORANGE}" fill-opacity="0.1" stroke="{ORANGE}"/><line x1="{cx}" y1="{cy - 10}" x2="{cx}" y2="{cy + 10}" stroke="{ORANGE}" stroke-width="0.6"/><line x1="{cx - 12}" y1="{cy}" x2="{cx + 12}" y2="{cy}" stroke="{ORANGE}" stroke-width="0.6"/>')
        elif kind == 'trigger': t.append(f'<rect x="{cx - 14}" y="{cy - 9}" width="28" height="18" fill="none" stroke="{ORANGE}" stroke-width="1.6" stroke-dasharray="3 4"/>')
        elif kind == 'boss': t.append(f'<polygon points="{cx},{cy - 12} {cx + 12},{cy} {cx},{cy + 12} {cx - 12},{cy}" fill="{RED}" stroke="#fff"/>')
        elif kind == 'enemy': t.append(f'<circle cx="{cx}" cy="{cy}" r="9" fill="{RED}" stroke="#fff"/>')
        elif kind == 'npc': t.append(f'<circle cx="{cx}" cy="{cy}" r="9" fill="{YEL}" stroke="#fff"/>')
        elif kind == 'chest': t.append(f'<rect x="{cx - 10}" y="{cy - 8}" width="20" height="16" fill="{GOLD}" stroke="#fff"/>')
        elif kind == 'shrine': t.append(f'<polygon points="{cx},{cy - 14} {cx + 10},{cy} {cx},{cy + 14} {cx - 10},{cy}" fill="{CYAN}" stroke="#fff"/>')
        elif kind == 'pickup': t.append(f'<polygon points="{cx},{cy - 8} {cx + 7},{cy} {cx},{cy + 8} {cx - 7},{cy}" fill="{GREEN}" stroke="#fff"/>')
        elif kind == 'light': t.append(f'<circle cx="{cx}" cy="{cy}" r="6" fill="{ORANGE}"/><circle cx="{cx}" cy="{cy}" r="12" fill="none" stroke="{ORANGE}"/>')
        elif kind == 'tree': t.append(f'<polygon points="{cx + 12},{cy} {cx + 6},{cy + 10} {cx - 6},{cy + 10} {cx - 12},{cy} {cx - 6},{cy - 10} {cx + 6},{cy - 10}" fill="{GREEN}" fill-opacity="0.22" stroke="{GREEN}"/>')
        t.append(f'<text x="{lx + 56}" y="{y + 5}" fill="{LINE}" font-family="{FONT}" font-size="14">{esc(lab)}</text>')
        y += 30
    y += 12
    t.append(f'<line x1="{lx + 16}" y1="{y}" x2="{W - 26}" y2="{y}" stroke="{FAINT}"/>')
    y += 28
    t.append(f'<text x="{lx + 16}" y="{y}" fill="{GOLD}" font-family="{FONT}" font-size="15" font-weight="bold" letter-spacing="2">HUOMIOT</text>')
    y += 26
    for n in z['plan_notes']:
        words, line, first = n.split(), '', True
        for w_ in words:
            if len(line) + len(w_) > 40:
                t.append(f'<text x="{lx + 16}" y="{y}" fill="#c8d6ec" font-family="{FONT}" font-size="13.5">{"• " if first else "  "}{esc(line.strip())}</text>')
                y += 19; line = ''; first = False
            line += w_ + ' '
        t.append(f'<text x="{lx + 16}" y="{y}" fill="#c8d6ec" font-family="{FONT}" font-size="13.5">{"• " if first else "  "}{esc(line.strip())}</text>')
        y += 26
    t.append(f'<text x="{lx + 16}" y="{H - 24}" fill="#7f97bb" font-family="{FONT}" font-size="11.5">Tarkat arvot: docs/zone-plans/{z["key"]}.md</text>')
    return head + ''.join(s.o) + ''.join(t) + '</svg>'


# ======================================================================== spec writer
def fnum(v):
    if isinstance(v, float) and abs(v - round(v)) < 1e-9:
        v = int(round(v))
    return f'{v:g}' if isinstance(v, float) else str(v)


def fpos(p):
    return '(' + ', '.join(fnum(c) for c in p) + ')'


def fsize(e):
    k = e['kind']
    if k == 'door':
        return f'aukko {fnum(e["gap"])} m, trigger {fnum(e["width"])} × 4 × 3'
    if k == 'light':
        t = {'point': 'piste', 'spot': 'spot', 'directional': 'suunta'}[e['ltype']]
        return f'{t}, {e["color"]}, range {fnum(e["range"])}, int {fnum(e["intensity"])}'
    if k == 'path':
        return f'leveys {fnum(e["width"])} m'
    if k == 'grid':
        return f'{e["tiles"]} × {e["tiles"]} ruutua × {fnum(e["tile"])} m'
    if e.get('r') and k in ('circle', 'pillar', 'tree'):
        h = f', korkeus {fnum(e["size"][1])}' if e.get('size') else ''
        return f'r {fnum(e["r"])} m{h}'
    if e.get('size'):
        return ' × '.join(fnum(c) for c in e['size'])
    return ''


def fwhere(e):
    k = e['kind']
    if k == 'path':
        return ' → '.join(f'({fnum(x)}, {fnum(z)})' for x, z in e['pts'])
    if k == 'region':
        return 'monikulmio ' + ', '.join(f'({fnum(x)}, {fnum(z)})' for x, z in e['pts'])
    if k == 'crack':
        return '; '.join(' → '.join(f'({fnum(x)}, {fnum(z)})' for x, z in pl) for pl in e['polylines'])
    if k == 'scatter':
        return 'polun reunoilla'
    return fpos(e['pos'])


KIND_FI = {'floor': 'lattia', 'wall': 'seinä', 'gate': 'lukittu portti', 'tree': 'puu', 'pickup': 'keräiltävä', 'light': 'valo',
           'spawn': 'spawn', 'door': 'ovi', 'path': 'polku', 'prop': 'esine', 'rock': 'kivi', 'scatter': 'sirottelu',
           'barrier': 'este', 'grid': 'taisteluruudukko', 'enemy': 'vihollinen', 'boss': 'pomo', 'npc': 'dialogi-NPC',
           'chest': 'arkku', 'trigger': 'laukaisualue', 'tower': 'torni', 'circle': 'pyöreä esine', 'banner': 'viiri',
           'brazier': 'hiillos', 'fx': 'efekti', 'window': 'ikkuna', 'pillar': 'pilari', 'shrine': 'alttari', 'torch': 'soihtu',
           'pedestal': 'jalusta', 'region': 'alue', 'dais': 'koroke', 'crack': 'halkeamat', 'text': ''}


def rows_for(els):
    """Merge grouped elements into one row."""
    out, seen = [], {}
    for e in els:
        g = e.get('group')
        if g and g in seen and e.get('was_rot') is None and not e.get('was_pos'):   # corrected values get their own rows
            seen[g]['positions'].append(e)
            continue
        row = dict(e=e, positions=[e])
        out.append(row)
        if g: seen[g] = row
    return out


def md_table(els, show_was=False):
    head = '| Nimi | Tyyppi | Sijainti (x, y, z) | Koko (x × y × z) | Rot Y | Collider | Rakennus / huomio |\n|---|---|---|---|---|---|---|\n'
    lines = []
    for row in rows_for(els):
        e = row['e']; ps = row['positions']
        name = e.get('group') if len(ps) > 1 else e['id']
        if len(ps) > 1:
            name = f'{e["group"]} ×{len(ps)}'
            rots = sorted({p['rot'] for p in ps})
            if len(rots) == 1:
                where = '; '.join(fpos(p['pos']) for p in ps)
                rot = fnum(rots[0])
            else:
                where = '; '.join(f'{fpos(p["pos"])} rot {fnum(p["rot"])}' for p in ps)
                rot = 'ks. sijainnit'

        else:
            where = fwhere(e)
            rot = fnum(e['rot'])
            if show_was and e.get('was_rot') is not None:
                rot = f'**{fnum(e["rot"])}** (nyt {fnum(e["was_rot"])})'
        if show_was and e.get('was_pos'):
            where = f'**{fpos(e["pos"])}** (nyt {fpos(e["was_pos"])})'
        col = e.get('collider')
        if col is None and e['kind'] in ('wall', 'floor', 'gate', 'barrier', 'dais'):
            col = True
        coll = 'kyllä' if col is True else ('ei' if col is False else '')
        notes = []
        if e['kind'] == 'door':
            notes.append(f'→ `{e["target"]}` / `{e["spawn"]}`; kehote "{e["prompt"]}"')
        if e['kind'] == 'light':
            if e.get('variant') and e['variant'] != 'both':
                notes.append(f'versio: {e["variant"]}')
        if e.get('build'):
            notes.append(e['build'])
        if e.get('desc'):
            notes.append(e['desc'])
        lines.append(f'| {name} | {KIND_FI.get(e["kind"], e["kind"])} | {where} | {fsize(e)} | {rot} | {coll} | {"<br>".join(esc(n) for n in notes)} |')
    return head + '\n'.join(lines) + '\n'


def write_zone_md(z, out_dir):
    d = z['directional']
    L = [f'# Zone {z["num"]} · {z["fi"]} (`{z["scene"]}`)', '',
         f'![Pohjapiirros](plans/{z["key"]}_plan.jpg)', '',
         'Konseptikuvat: ' + ' · '.join(f'[{c}](concepts/{c}.jpg)' for c in z['concepts']), '',
         z['summary'], '',
         f'Pelaajan aloituspaikka scenessä {fpos(z["player_start"])}. Directional nyt: väri {d["color"]}, int {fnum(d["intensity"])}, rotaatio {fpos(d["rot"])}.', '']
    if z['local']:
        L += ['## Paikallisen masterin erot (käytä niitä)', ''] + [f'- {t}' for t in z['local']] + ['']
    groups = [(NYK, 'Nykyiset (GitHub master, pidä ennallaan)', False),
              (KORJ, 'Korjaukset (olemassa oleva objekti, muutettu arvo)', True),
              (PAIK, 'Paikallisen masterin hallinnoimat (älä muuta tämän piirroksen mukaan)', False),
              (UUSI, 'Uudet (rakennetaan konseptikuvien mukaan)', False)]
    for st, title, was in groups:
        els = [e for e in z['elements'] if e['status'] == st and not e.get('draw_only')]
        if not els: continue
        L += [f'## {title}', '', md_table(els, show_was=was)]
    L += ['## Valaistus ja tunnelma', '', f'Oletus: **{z["mood"]["default"]}**.', '']
    for title, items in z['mood']['items']:
        L += [f'**{title}**', ''] + [f'- {i}' for i in items] + ['']
    L += ['## Pelilliset huomiot', ''] + [f'- {g}' for g in z['gameplay']] + ['']
    open(os.path.join(out_dir, f'{z["key"]}.md'), 'w').write('\n'.join(L))


def main(out_dir):
    os.makedirs(os.path.join(out_dir, 'plans_svg'), exist_ok=True)
    for z in ZONES:
        s = draw_zone(z)
        open(os.path.join(out_dir, 'plans_svg', f'{z["key"]}_plan.svg'), 'w').write(legend_and_frame(s, z))
        write_zone_md(z, out_dir)
    data = []
    for z in ZONES:
        zz = {k: v for k, v in z.items() if k not in ('plan_notes',)}
        zz['elements'] = [{k: v for k, v in e.items() if k not in ('lab', 'side', 'draw_only') and v is not None}
                          for e in z['elements'] if not e.get('draw_only')]
        data.append(zz)
    json.dump(dict(units='metres', axes='Unity world: X east, Y up, Z north; rot = Y degrees',
                   source='Assets/Scripts/Editor/ZoneSceneBuilder.cs + SetupRiggedEnemiesEditor.cs (DressZone2) @ GitHub master 8ae7140',
                   statuses={NYK: 'exists on GitHub master', UUSI: 'new, from concept art', KORJ: 'existing object, corrected value',
                             PAIK: 'owned by local master (commander-door-grid); keep local values'},
                   zones=data), open(os.path.join(out_dir, 'zone-plans.json'), 'w'), ensure_ascii=False, indent=1)
    print('ok', out_dir)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'out')
