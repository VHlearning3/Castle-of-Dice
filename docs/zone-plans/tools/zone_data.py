"""Single source of truth for the zone 2-7 floor plans and build specs.

Coordinates are Unity world metres: X = east (right on the plan), Z = north (up on the plan), Y = up.
rot = Unity Y rotation in degrees (0 = faces +Z/north, 90 = faces +X/east).
'nykyinen' values are copied from Assets/Scripts/Editor/ZoneSceneBuilder.cs on GitHub master (8ae7140).
For 'uusi' items pos is the footprint centre on the floor (y = 0) unless a height is given.
"""

NYK, UUSI, KORJ, PAIK = 'nykyinen', 'uusi', 'korjaus', 'paikallinen'


def E(kind, id, status, pos, size=None, rot=0, **kw):
    d = dict(kind=kind, id=id, status=status, pos=tuple(pos), size=tuple(size) if size else None, rot=rot)
    d.update(kw)
    return d


def rgb(r, g, b):
    return '#%02x%02x%02x' % (round(r*255), round(g*255), round(b*255))


def pillars(prefix, half_x, half_z, height, status=NYK, label=None):
    """Mirror of ZoneSceneBuilder.CreateCornerPillars: Pillar.prefab, scale (1.2, height/5, 1.2)."""
    out = []
    for i, (sx, sz) in enumerate(((-1, 1), (1, 1), (-1, -1), (1, -1))):
        out.append(E('pillar', f'{prefix}_{i}', status, (sx*half_x, 0, sz*half_z), r=1.2, group=prefix,
                     size=(1.2, height, 1.2), build=f'Pillar.prefab, scale (1.2, {height/5:g}, 1.2)', collider=True,
                     label=None))
    return out


def door(id, pos, rot, target, spawn, width, gap, prompt, label, axis, outward, **kw):
    return E('door', id, NYK, pos, rot=rot, target=target, spawn=spawn, width=width, gap=gap, prompt=prompt,
             label=label, axis=axis, outward=outward,
             build=f'CreateSceneDoorway: Arch.prefab x1.4 + 2x Pillar.prefab, DoorTeleporter-trigger {width:g} x 4 x 3 m',
             **kw)


def spawn(id, pos, yaw, side='r'):
    return E('spawn', id, NYK, pos, rot=yaw, side=side, build='StartSpawnPoint + trigger 2 x 2 x 2')


def light(id, status, pos, color, rng, inten, ltype='point', variant='both', note='', **kw):
    return E('light', id, status, pos, color=color, range=rng, intensity=inten, ltype=ltype, variant=variant,
             desc=note, **kw)


# ======================================================================================
ZONES = []

# ------------------------------------------------------------------ Zone 2: Forest Path
z2 = dict(
    key='02_forest_path', num=2, scene='Zone_2_ForestPath', fi='Metsäpolku', en='Forest Path',
    extent=(80, 110), center=(0, 0), step=5,
    subtitle='Forest Path · 80 × 110 m · valinnainen zombitaistelu',
    concepts=['02a_forest_path_day', '02b_forest_path_night'],
    summary='Mutkitteleva metsäpolku kylästä (etelä) linnan alapihalle (pohjoinen). Länteen haarautuu sivupolku '
            'tiirikoitavalle salaportille (DC 13), jonka takaa aukeaa salareitti kirjastoon.',
    directional=dict(color=rgb(0.75, 0.88, 0.70), intensity=1.1, rot=(50, -30, 0)),
    player_start=(0, 0.5, -44),
)
z2['elements'] = [
    E('floor', 'Forest_Ground', NYK, (0, -0.5, 0), (80, 1, 110), build='Cube, M_Fern.mat'),
    E('wall', 'Forest_Wall_West', NYK, (-40, 3, 0), (2, 6, 110), build='Cube, M_Ruined_walls.mat'),
    E('wall', 'Forest_Wall_East', NYK, (40, 3, 0), (2, 6, 110), build='Cube, M_Ruined_walls.mat'),
    E('wall', 'Forest_Wall_South_L', NYK, (-24, 3, -54), (32, 6, 2), build='Cube'),
    E('wall', 'Forest_Wall_South_R', NYK, (24, 3, -54), (32, 6, 2), build='Cube'),
    E('wall', 'Forest_Wall_North_L', NYK, (-24, 3, 54), (32, 6, 2), build='Cube'),
    E('wall', 'Forest_Wall_North_R', NYK, (24, 3, 54), (32, 6, 2), build='Cube'),
]
for i, (x, z) in enumerate(((-18, -35), (-28, -15), (-20, 10), (-25, 35), (18, -38), (26, -12), (22, 15), (28, 38), (-12, -10), (14, 2))):
    z2['elements'].append(E('tree', f'Pine_tree ({i+1})', NYK, (x, 0, z), r=2.0, group='Pine_tree (nykyiset)',
                            build='Pine_tree.prefab (Flora_And_Flanks)', collider=True))
z2['elements'] += [
    E('pickup', 'SwampHerb_1', NYK, (-12, 0.4, -20), label='Suoyrtti', side='l', group='SwampHerb',
      build='Sphere 0.8 + ChestRewardInteraction (Item_SwampHerb), Mirabelin tehtävä'),
    E('pickup', 'SwampHerb_2', NYK, (15, 0.4, 8), group='SwampHerb', build='sama'),
    E('pickup', 'SwampHerb_3', NYK, (-8, 0.4, 32), group='SwampHerb', build='sama'),
    light('ForestLight_South', NYK, (0, 6, -25), rgb(0.55, 0.85, 0.45), 30, 2.0, note='shadows Soft'),
    light('ForestLight_North', NYK, (0, 6, 25), rgb(0.45, 0.75, 0.65), 30, 2.0, note='shadows Soft'),
    spawn('Spawn_From_Village', (0, 0.5, -44), 0),
    spawn('Spawn_From_Courtyard', (0, 0.5, 44), 180),
    spawn('Spawn_From_Library', (-28, 0.5, 15), 90, side='b'),
    door('Door_Forest_Village', (0, 0, -53), 180, 'Zone_1_VillageAndCellar', 'Spawn_From_Forest', 8, 16,
         'Return to Oakhaven Village (Village)', '↓ Kylä', 'x', -1),
    door('Door_Forest_Courtyard', (0, 0, 53), 0, 'Zone_3_CastleCourtyard', 'Spawn_From_Forest', 8, 16,
         'Enter Castle Courtyard (Wing 1)', '↑ Linnan alapiha', 'x', 1),
    door('Door_Forest_Library_Secret', (-33, 0, 15), -90, 'Zone_4_Library', 'Spawn_From_SecretPath', 6, 6,
         'Slip into the Grand Archives (Secret Path)', 'Kirjasto (salareitti)', 'z', -1,
         desc='SetActive(false) kunnes portti on tiirikoitu (LockpickInteraction.hiddenPathObject).'),
    E('gate', 'Locked_Secret_Gate', NYK, (-25, 2.5, 15), (1.2, 5, 6), label='Lukittu salaportti (DC 13)',
      build='Cube + LockpickInteraction: DC 13, rewardGold 25, hasTrap, trapDamage 4', collider=True),
    E('grid', 'CombatGrid_Forest', NYK, (0, 0.05, -24), tiles=12, tile=1.6,
      build='CombatGrid.prefab, Forest_Zombie_Encounter-objektin alla (SetupRiggedEnemiesEditor.DressZone2)',
      desc='Pidä ruudukon alue (x -9.6…9.6, z -33.6…-14.4) vapaana uusista kiinteistä esineistä.'),
    E('enemy', 'Forest_Zombie', NYK, (0, 1.5, -18), rot=180, label='Zombi', side='r',
      build='Enemy_Zombie.prefab "Rotting Zombie" HP 20, AC 11, DMG 4; StandOnGround; SetActive(false) kunnes taistelu (DressZone2)'),
    E('trigger', 'Forest_Zombie_Trigger', NYK, (0, 2.5, -24), (20, 6, 14), label='zombin laukaisualue',
      build='BoxCollider trigger + DungeonRoomController (roomLocation "Forest", ei pomoa) (DressZone2)'),
    # ---------------- new
    E('path', 'Forest_Path_Main', UUSI, (0, 0, 0), pts=[(0, -54), (3, -40), (-4, -25), (2, -8), (-3, 5), (-6, 15), (0, 30), (4, 42), (0, 54)],
      width=6, label='Mutkitteleva metsäpolku', lab=(11.5, -29, 'start'),
      build='Tasainen polkumateriaali (ruskea #c89a5c) maahan: litteät kuutiot 0.02 m tai Tile_*.prefab-kivet; reunoille Grass/Fern',
      collider=False, desc='Leveys 6 m, kulkee ovelta ovelle.'),
    E('path', 'Forest_Path_Branch', UUSI, (0, 0, 0), pts=[(-6, 15), (-14, 17), (-22, 15), (-33, 15)], width=3.5,
      label='Sivupolku', lab=(-13, 12.6, 'middle'), build='sama kuin pääpolku, kapeampi', collider=False,
      desc='Haarautuu pääpolulta (-6, 15) salaportille ja salaovelle.'),
    E('prop', 'Gate_Arch_Ivy', UUSI, (-25, 0, 15), (8, 6.5, 2), rot=90, label=None,
      build='Arch.prefab (rot 90 kuten sivuovien kaaret) skaala ~1.5 portin ympärille + Leaves_*/Bush_*-muratti; lukko kultaisena (emissive)',
      collider=False, desc='Pelkkä kehys: Locked_Secret_Gate on edelleen se, joka estää kulun.'),
]
for i, (x, z) in enumerate(((-33, -45), (-30, -28), (-34, 0), (-30, 28), (-34, 45), (33, -45), (31, -25), (34, 0), (32, 26), (34, 46),
                            (-12, 40), (14, 30), (13.5, -20), (-14, -42), (16, -48), (-20, 46))):
    z2['elements'].append(E('tree', f'Pine_Cluster_{i+1:02d}', UUSI, (x, 0, z), r=2.6, group='Pine_Cluster (uudet)',
                            build='2–3 kpl Pine_tree.prefab / Pine_tree_2.prefab r ≤ 3 m sisällä, satunnainen rot, skaala 0.9–1.3',
                            collider=True))
for i, (x, z, w, d) in enumerate(((12.5, -35, 3, 2), (-8, 22, 2.4, 1.8), (9, 20, 2, 1.6), (-9, -44, 3, 2.2))):
    z2['elements'].append(E('rock', f'Rock_Group_{i+1}', UUSI, (x, 0, z), (w, 1.5, d), group='Rock_Group',
                            build='Rock_1…4.prefab', collider=True, label='kiviä' if i == 0 else None, lab=(12.5, -38.3, 'middle')))
z2['elements'] += [
    E('prop', 'Lantern_Post', UUSI, (4.5, 0, -2), (0.6, 3.2, 0.6), label=None,
      build='Lamppost.prefab tai street_light.prefab', collider=True),
    light('Lantern_Light', UUSI, (4.5, 2.8, -2), '#ffb04a', 10, 1.6, variant='B (yö)',
          note='Päällä vain yöversiossa; shadows None', label='Lyhtypylväs', side='r'),
    light('Gate_Lock_Glow', UUSI, (-24.2, 2.6, 15), '#ffd76a', 6, 1.2, variant='A kulta / B syaani #5ae0ff',
          note='Lukon hehku; shadows None'),
    E('scatter', 'Path_Edge_Scatter', UUSI, (0, 0, 0), draw=False,
      build='~20 kpl Mushroom_1…4, Flowers_1/2, Fern, Grass_1…3 polun reunoille 1–5 m päähän reunasta',
      collider=False, desc='Ei collidereita (ei estä kävelyä).'),
]
z2['local'] = [
    'Tiirikointi on paikallisessa masterissa minipeli (PR #10). Portin paikka ja DC pysyvät samoina.',
]
z2['gameplay'] = [
    'Zombin taistelu (Forest_Zombie_Encounter: ruudukko, zombi ja laukaisualue) tulee SetupRiggedEnemiesEditor.DressZone2:sta, '
    'ei ZoneSceneBuilderista. Jos Zone 2 rakennetaan uudelleen, aja sen jälkeen CastleOfDice/Setup Rigged Enemies, '
    'Bosses & Heroes, muuten zombi puuttuu.',
    'Älä sulje salaportin takaista käytävää pensailla tai kivillä. Kirjaston kautta saapuva sankari ilmestyy '
    'pisteeseen (-28, 15) portin taakse, ja hänen on päästävä pois myös silloin, kun porttia ei ole tiirikoitu '
    '(tiirikointi on vain Varkaalle).',
    'Pidä polku ja ovien edustat (16 m aukot) vapaina kiinteistä esineistä.',
    'Kamera seuraa lounaasta (offset -6, 12, -12). Yli 4 m korkeat esineet polun eteläpuolella voivat peittää sankarin.',
]
z2['mood'] = dict(
    default='A · Päivä',
    items=[
        ('A · Päivä (oletus)', [
            'Directional: väri #fff1cc, intensiteetti 1.25, rotaatio (45, -35, 0).',
            'Ambient (Gradient): taivas #9ccbea, horisontti #bfd8a0, maa #4e6b3a.',
            'Sumu: Linear #dce8d2, alku 40 m, loppu 130 m.',
            'Nykyiset vihreät pistevalot jäävät, intensiteetti 2.0 → 1.2.',
            'Auringonsäteet: 5–7 additiivista läpinäkyvää tasoa (#fff6cf, alpha ≈ 0.12) lounaasta koilliseen.',
            'Partikkelit: leijuva siitepöly/lehdet, 20–40 kpl, hidas.',
        ]),
        ('B · Kuutamoyö', [
            'Directional (kuu): #8fa6e0, intensiteetti 0.35, rotaatio (35, 150, 0).',
            'Ambient: Flat #1c2244. Skybox tumma #0b1430 + tähdet.',
            'Sumu: Exponential Squared #5a6a98, tiheys 0.03.',
            'Lantern_Light päälle (#ffb04a, range 10, int 1.6); portin riimut syaanina #5ae0ff.',
            'Tulikärpäset: 30 partikkelia #d8ff7a, koko 0.06–0.12.',
        ]),
    ])
z2['plan_notes'] = [
    'Nykyiset 10 puuta (valkoinen) jäävät; uudet mäntyryhmät (vihreä) reunustavat polkua.',
    'Sivupolku vie salaportille (-25, 15). Ovi kirjastoon (-33, 15) näkyy vasta, kun lukko on tiirikoitu.',
    'Portin takaista käytävää ei saa sulkea: kirjastosta palaava ilmestyy pisteeseen (-28, 15).',
    'Zombin taistelu (nykyinen) pysyy paikallaan; sen ruudukon alue pidetään vapaana uusista esineistä.',
    'Lyhty ja riimuhehku kuuluvat yöversioon.',
]
ZONES.append(z2)

# --------------------------------------------------------------- Zone 3: Castle Courtyard
z3 = dict(
    key='03_courtyard', num=3, scene='Zone_3_CastleCourtyard', fi='Linnan alapiha', en='Castle Courtyard',
    extent=(90, 90), center=(0, 0), step=5,
    subtitle='Castle Courtyard · 90 × 90 m · 1. pomo: Kirottu Komentaja',
    concepts=['03a_courtyard_storm', '03b_courtyard_sunset'],
    summary='Muurien ympäröimä piha. Kirottu Komentaja vartioi pohjoisovea keskushalliin, ja taistelun '
            'laukaisualue ulottuu seinästä seinään ja ovelle asti. Etelässä kirottu suihkulähde, sivuilla harjoituspiha.',
    directional=dict(color=rgb(1, 0.95, 0.85), intensity=1.2, rot=(50, -30, 0)),
    player_start=(0, 0.5, -42),
)
z3['elements'] = [
    E('floor', 'Courtyard_Floor', NYK, (0, -0.5, 0), (90, 1, 90), build='Cube, M_Ruined_walls.mat'),
    E('wall', 'Courtyard_Wall_West', NYK, (-45, 4, 0), (2, 8, 90), build='Cube'),
    E('wall', 'Courtyard_Wall_East', NYK, (45, 4, 0), (2, 8, 90), build='Cube'),
    E('wall', 'Courtyard_Wall_South_L', NYK, (-25, 4, -45), (40, 8, 2), build='Cube'),
    E('wall', 'Courtyard_Wall_South_R', NYK, (25, 4, -45), (40, 8, 2), build='Cube'),
    E('wall', 'Courtyard_Wall_North_L', NYK, (-25, 4, 45), (40, 8, 2), build='Cube'),
    E('wall', 'Courtyard_Wall_North_R', NYK, (25, 4, 45), (40, 8, 2), build='Cube'),
] + pillars('Courtyard_Pillar', 42, 42, 8) + [
    light('CourtyardLight_Center', NYK, (0, 7, 0), rgb(1, 0.75, 0.45), 35, 2.5, note='shadows Soft'),
    light('CourtyardLight_North', NYK, (0, 6, 25), rgb(1, 0.65, 0.35), 28, 2.0, note='shadows Soft'),
    spawn('Spawn_From_Forest', (0, 0.5, -40), 0),
    spawn('Spawn_From_Hall', (0, 0.5, 40), 180, side='l'),
    door('Door_Courtyard_Forest', (0, 0, -44.5), 180, 'Zone_2_ForestPath', 'Spawn_From_Courtyard', 8, 10,
         'Return to Forest Path (Forest)', '↓ Metsäpolku', 'x', -1),
    door('Door_Courtyard_CastleHall', (0, 0, 44.5), 0, 'Zone_5_CastleHall', 'Spawn_From_Courtyard', 8, 10,
         'Enter Great Central Hall (Safe Haven Hub)', '↑ Keskushalli', 'x', 1),
    E('barrier', 'Barrier_South', NYK, (0, 3.5, -44), (10, 7, 1.5), build='Cube M_HammerJAanvil, SetActive(false) kunnes taistelu alkaa'),
    E('barrier', 'Barrier_North', NYK, (0, 3.5, 44), (10, 7, 1.5), build='sama; ehdotus: ulkoasu rautaristikoksi (portcullis)'),
    E('grid', 'CombatGrid_Courtyard', NYK, (0, 0.05, 10), tiles=12, tile=1.6,
      build='CombatGrid.prefab', desc='Paikallisessa masterissa 32 × 32 ja liukuu sankarin luo (ks. erot).'),
    E('enemy', 'Courtyard_Skeleton', NYK, (-6, 0.9, 12), label='Luurankovartija', side='l',
      build='EnemyUnit "Armored Skeleton Guard" HP 20, AC 12, DMG 4; SetActive(false) kunnes taistelu'),
    E('boss', 'Boss_CursedCommander', NYK, (0, 1.2, 18), label='Kirottu Komentaja', side='r',
      build='CursedCommanderBoss; SetActive(false) kunnes taistelu'),
    E('npc', 'NPC_CursedCommander', PAIK, (0, 1.0, -12), label='dialogi-NPC (paik. masterissa pomon paikalla)', side='r',
      build='VillageNPC + Commander_Intro.asset', desc='Paikallisessa masterissa NPC seisoo pomon taistelupaikalla ja dialogi alkaa automaattisesti laukaisualueelle astuttaessa.'),
    E('chest', 'Courtyard_Reward_Chest', NYK, (0, 0.4, 30), label='Arkku (40 g)', side='r',
      build='Chest.prefab + ChestRewardInteraction 40 g; näkyy voiton jälkeen'),
    E('trigger', 'Courtyard_Encounter_Trigger', PAIK, (0, 2.5, 19), (88, 6, 50),
      build='BoxCollider trigger + DungeonRoomController (CursedCommander)',
      desc='GitHub masterissa (0, 2.5, 10) koko 32 × 6 × 32. Paikallisessa masterissa seinästä seinään x -44…44, z -6…44, eli ovelle asti (Vilin pyyntö 3.10.).',
      label='taistelun laukaisualue: seinästä seinään, ovelle asti'),
    # ---------------- new
    E('tower', 'Corner_Tower_NW', UUSI, (-41, 0, 41), (9, 14, 9), label='Torni', group='Corner_Tower',
      build='Tower.prefab tai kuutiot + harjakaiteet; ympäröi kulmapilarin', collider=True),
    E('tower', 'Corner_Tower_NE', UUSI, (41, 0, 41), (9, 14, 9), label='Torni', group='Corner_Tower', build='sama', collider=True),
    E('tower', 'Corner_Tower_SW', UUSI, (-41, 0, -41), (9, 14, 9), label='Torni', group='Corner_Tower', build='sama', collider=True),
    E('tower', 'Corner_Tower_SE', UUSI, (41, 0, -41), (9, 14, 9), label='Torni', group='Corner_Tower', build='sama', collider=True),
    E('tower', 'Gatehouse_Tower_W', UUSI, (-7, 0, 41), (4, 10, 6), label=None, group='Gatehouse_Tower',
      build='kuutiot + harjakaiteet, oven molemmin puolin', collider=True),
    E('tower', 'Gatehouse_Tower_E', UUSI, (7, 0, 41), (4, 10, 6), label='Porttitorni', lab=(9.5, 37.3, 'start'), group='Gatehouse_Tower',
      build='sama', collider=True),
    E('circle', 'Cursed_Fountain', UUSI, (0, 0, -26), r=5.5, color='#6affb0', label='Kirottu suihkulähde',
      size=(11, 0.8, 11), build='Allas: matala sylinteri r 5.5, korkeus 0.8; keskellä murtunut patsas 3.5 m; '
      'vesi vihreä emissive #3aa86a', collider=True),
    light('Fountain_Glow', UUSI, (0, 1.5, -26), '#6aff9a', 12, 1.5, note='shadows None'),
] + [
    E('banner', f'Banner_North_{i+1}', UUSI, (x, 4.5, 43.8), (3, 6, 0.2), group='Banner_North', color='#c89bff',
      build='Revitty viiri #3a1f4a, vihreä tunnus #8affb0; riippuu 1.5–7.5 m korkeudella seinässä', collider=False,
      label='revityt viirit' if i in (0, 2) else None, lab=((x+6) if i in (0, 2) else x, 40.6, 'middle'))
    for i, x in enumerate((-30, -18, 18, 30))
] + [
    E('circle', f'Training_Dummy_{i+1}', UUSI, (-36, 0, z), r=1.3, rot=90, size=(1.2, 2.2, 1.2), group='Training_Dummy',
      build='Tolppa + poikkipuu + olkivartalo (Log-, Box-prefabit tai kuutiot)', collider=True,
      label='olkinuket' if i == 0 else None, lab=(-36, -34.6, 'middle'))
    for i, z in enumerate((-30, -20, -10))
] + [
    E('prop', 'Weapon_Rack_W', UUSI, (-42, 0, 12), (1.2, 2.2, 12), label='asetelineet', lab=(-38.5, 20, 'start'),
      group='Weapon_Rack', build='Puuteline + miekat/keihäät (Log + ohuet kuutiot)', collider=True),
    E('prop', 'Weapon_Rack_E', UUSI, (42, 0, 12), (1.2, 2.2, 12), label='asetelineet', lab=(38.5, 20, 'end'),
      group='Weapon_Rack', build='sama', collider=True),
] + [
    E('brazier', f'Brazier_{i+1}', UUSI, (x, 0, z), (1.2, 1.4, 1.2), group='Brazier',
      build='Maljahiillos: Cauldron.prefab tai sylinteri + tulipartikkelit', collider=True,
      label='hiillokset' if i == 3 else None, lab=(14, 24.4, 'start'))
    for i, (x, z) in enumerate(((-12, -2), (12, -2), (-12, 22), (12, 22)))
] + [
    light(f'Brazier_Light_{i+1}', UUSI, (x, 1.8, z), '#ff9a3a', 10, 1.6, note='shadows None, värinä', group='Brazier_Light', draw=False)
    for i, (x, z) in enumerate(((-12, -2), (12, -2), (-12, 22), (12, 22)))
] + [
    E('fx', 'Rain_And_Lightning', UUSI, (0, 0, 0), draw=False, variant='A (myrsky)',
      build='Sadepartikkelit kameran mukana (~600 viirua #c8d4e8, alpha 0.35); salama 8–15 s välein: directional 0.75 → 2.4 '
      'kahdesti 0.08 s; ukkosääni SFXManager-poolin kautta', collider=False),
]
z3['local'] = [
    'Laukaisualue on paikallisessa masterissa jo seinästä seinään (x -44…44, z -6…44), joten keskushallin ovelle ei pääse ilman taistelua (ketju "Commander door trigger and bigger grids").',
    'Taisteluruudukko on paikallisessa masterissa 32 × 32 (51,2 m) ja liukuu niin, että sankari on vähintään 2 ruutua reunan sisäpuolella.',
    'Dialogi-NPC seisoo paikallisessa masterissa pomon taistelupaikalla, ja dialogi alkaa aina, kun sankari astuu laukaisualueelle. Voitetun pomon NPC ei näy.',
    'Käytä näissä paikallisen masterin arvoja. Tämä piirros ei muuta laukaisualuetta, ruudukkoa, pomoa eikä NPC:tä.',
]
z3['gameplay'] = [
    'Ruudukon alle jäävä kiinteä esine (collider 0.1–1.9 m korkeudella) tekee ruudusta kulkukelvottoman (GridManager.cs, OverlapBox). '
    'Hiillokset ovat tarkoituksella 1 ruudun suojia; muut uudet esineet ovat reunoilla.',
    'Pidä oven edusta (x -5…5, z 30…44) ja Spawn_From_Hall (0, 40) vapaina.',
    'Suihkulähde katkaisee suoran linjan etelästä pohjoiseen, mutta sen molemmin puolin jää yli 15 m kulkutilaa.',
    'Taisteluun ei lisätä vihollisia (sääntö: 1 eliitti tai enintään 2 vihollista, pomolla enintään 1 apulainen).',
    'Barrier_North voi näyttää rautaristikolta (portcullis), koska se näkyy vain taistelun aikana.',
]
z3['mood'] = dict(
    default='A · Myrsky',
    items=[
        ('A · Myrsky (oletus)', [
            'Directional: #9aa8bf, intensiteetti 0.75, rotaatio (50, -30, 0).',
            'Ambient: taivas #3a4452, maa #2e3440. Skybox harmaa myrsky.',
            'Sumu: Linear #5e6672, 25–110 m.',
            'Sade ja salama (Rain_And_Lightning). Suihkulähteen vihreä hehku #6aff9a ja Komentajan vihreä kirous.',
            'Hiillokset (#ff9a3a) lämpiminä vastapainoina.',
        ]),
        ('B · Auringonlasku', [
            'Directional: #ffb070, intensiteetti 1.2, matala kulma, rotaatio (15, 200, 0), pitkät varjot.',
            'Ambient: taivas #3a2a5a, horisontti #ffb060, maa #4a3a44.',
            'Ei sadetta. Kevyt usva #f0a070, 60–160 m.',
        ]),
    ])
z3['plan_notes'] = [
    'Laukaisualue ulottuu seinästä seinään ja ovelle asti (paikallinen master).',
    'Suihkulähde sisääntulon ja taistelualueen välissä, kuten konseptissa.',
    'Harjoituspiha (olkinuket, asetelineet) reunoilla; hiillokset ovat 1 ruudun suojia.',
    'Ruudukko on paikallisessa masterissa 32 × 32 ja liukuu sankarin luo.',
]
ZONES.append(z3)

# ------------------------------------------------------------------------ Zone 4: Library
z4 = dict(
    key='04_library', num=4, scene='Zone_4_Library', fi='Kirjasto', en='Library',
    extent=(90, 90), center=(0, 0), step=5,
    subtitle='Library · 90 × 90 m · 2. pomo: Varjomaagi Malakor',
    concepts=['04a_library_arcane', '04b_library_moon'],
    summary='Salatieteen siipi. Sisään lännestä salareitiltä tai idästä keskushallista. Hyllykäytävät kulmissa, '
            'loitsupiiri Malakorin alla, suuri lasimaalausikkuna pohjoisseinässä ja pitkä lukupöytä etelässä.',
    directional=dict(color=rgb(0.35, 0.55, 0.95), intensity=1.0, rot=(50, -30, 0)),
    player_start=(-35, 0.5, 0),
)
z4['elements'] = [
    E('floor', 'Library_Floor', NYK, (0, -0.5, 0), (90, 1, 90), build='Cube, M_Ruined_walls.mat'),
    E('wall', 'Library_Wall_North', NYK, (0, 4, 45), (90, 8, 2), build='Cube'),
    E('wall', 'Library_Wall_South', NYK, (0, 4, -45), (90, 8, 2), build='Cube'),
    E('wall', 'Library_Wall_West_L', NYK, (-45, 4, -25), (2, 8, 40), build='Cube'),
    E('wall', 'Library_Wall_West_R', NYK, (-45, 4, 25), (2, 8, 40), build='Cube'),
    E('wall', 'Library_Wall_East_L', NYK, (45, 4, -25), (2, 8, 40), build='Cube'),
    E('wall', 'Library_Wall_East_R', NYK, (45, 4, 25), (2, 8, 40), build='Cube'),
] + pillars('Library_Pillar', 40, 40, 8) + [
    E('wall', 'Bookshelf_1', NYK, (-20, 3.5, 25), (12, 7, 2.5), label='Kirjahylly', lab=(-20, 28.6, 'middle'), group='Bookshelf', build='Cube, M_Logs.mat'),
    E('wall', 'Bookshelf_2', NYK, (20, 3.5, 25), (12, 7, 2.5), label='Kirjahylly', lab=(20, 28.6, 'middle'), group='Bookshelf', build='Cube, M_Logs.mat'),
    E('wall', 'Bookshelf_3', NYK, (-20, 3.5, -25), (12, 7, 2.5), group='Bookshelf', build='Cube, M_Logs.mat'),
    E('wall', 'Bookshelf_4', NYK, (20, 3.5, -25), (12, 7, 2.5), group='Bookshelf', build='Cube, M_Logs.mat'),
    light('ArcaneLight_Center', NYK, (0, 6.5, 0), rgb(0.25, 0.55, 1.0), 32, 2.5, note='shadows Soft'),
    light('ArcaneLight_East', NYK, (20, 5, 15), rgb(0.45, 0.25, 0.95), 22, 2.0, note='shadows Soft'),
    light('ArcaneLight_West', NYK, (-20, 5, 15), rgb(0.45, 0.25, 0.95), 22, 2.0, note='shadows Soft'),
    spawn('Spawn_From_SecretPath', (-38, 0.5, 0), 90, side='b'),
    spawn('Spawn_From_Hall', (38, 0.5, 0), -90, side='b'),
    door('Door_Library_SecretPath', (-44.5, 0, 0), -90, 'Zone_2_ForestPath', 'Spawn_From_Library', 6, 10,
         'Slip through Secret Passage to Whispering Woods', 'Metsäpolku (salareitti)', 'z', -1),
    door('Door_Library_CastleHall', (44.5, 0, 0), 90, 'Zone_5_CastleHall', 'Spawn_From_Library', 6, 10,
         'Enter Great Central Hall (Safe Haven Hub)', 'Keskushalli', 'z', 1),
    E('barrier', 'Barrier_West', NYK, (-44, 3.5, 0), (1.5, 7, 8), build='Cube M_HammerJAanvil, näkyy taistelun ajan'),
    E('barrier', 'Barrier_East', NYK, (44, 3.5, 0), (1.5, 7, 8), build='sama'),
    E('grid', 'CombatGrid_Library', NYK, (0, 0.05, 10), tiles=12, tile=1.6, build='CombatGrid.prefab',
      desc='Paikallisessa masterissa 20 × 20 ja liukuu sankarin luo.'),
    E('enemy', 'Shadow_Decoy', NYK, (6, 0.9, 12), label='Varjohuijari', side='r',
      build='EnemyUnit "Shadow Decoy" HP 18, AC 12, DMG 4'),
    E('boss', 'Boss_ShadowMageMalakor', NYK, (0, 1.1, 18), label='Malakor', side='l', build='ShadowMageMalakorBoss'),
    E('npc', 'NPC_Malakor', PAIK, (0, 1.0, -12), label='dialogi-NPC (paik. masterissa pomon paikalla)', side='r',
      build='VillageNPC + Malakor_Intro.asset'),
    E('chest', 'Library_Reward_Chest', NYK, (0, 0.4, 30), label='Arkku (60 g + iso juoma)', side='t',
      build='Chest.prefab, 60 g + Item_GreaterPotion; näkyy voiton jälkeen'),
    E('trigger', 'Library_Encounter_Trigger', NYK, (0, 2.5, 10), (32, 6, 32),
      build='BoxCollider trigger + DungeonRoomController (ShadowMageMalakor)',
      desc='Paikallisessa masterissa seinästä seinään (tarkka z-väli siellä).',
      label='laukaisualue (paik. masterissa seinästä seinään)'),
    # ---------------- new
] + [
    E('prop', f'Shelf_Aisle_{n}', UUSI, (x, 0, z), (2.5, 7, 18), group='Shelf_Aisle',
      build='Kuten Bookshelf_*: runko M_Logs + kirjarivit (ohuet värilliset kuutiot)', collider=True,
      label='hyllykäytävät' if n in ('NW1', 'NE1') else None, lab=((-33 if x < 0 else 33), 43, 'middle'))
    for n, x, z in (('NW1', -36, 32), ('NW2', -30, 32), ('NE1', 36, 32), ('NE2', 30, 32),
                    ('SW1', -36, -32), ('SW2', -30, -32), ('SE1', 36, -32), ('SE2', 30, -32))
] + [
    E('window', 'Stained_Glass_Window', UUSI, (0, 4.25, 43.85), (14, 6.5, 0.3), color='#9ab8ff',
      label='Suuri lasimaalausikkuna', lab=(0, 41.2, 'middle'),
      build='Kehys + emissive-lasiruudut (#5a7ad0, #8aa8f0, #c87a9a, #7ac0c0, #d0c07a)', collider=False),
    light('Window_Moon_Spot', UUSI, (0, 7.5, 42), '#9ab8ff', 30, 2.0, ltype='spot', variant='B (kuunvalo)',
          note='Spot, rot (35, 180, 0), kulma 50°, shadows None', draw=False),
    E('prop', 'Reading_Table', UUSI, (0, 0, -34), (14, 0.9, 4), label='Lukupöytä + kynttilät', lab=(0, -39, 'middle'),
      build='Pöytä 14 × 0.9 × 3 + penkit molemmin puolin; kirjoja, kääröjä ja kynttilöitä (emissive)', collider=True),
    light('Reading_Candles', UUSI, (0, 1.6, -34), '#ffbf6a', 10, 1.2, note='shadows None'),
    E('prop', 'Ladder', UUSI, (-37.8, 0, 36), (1, 6, 0.4), rot=90, label='tikkaat', lab=(-41, 30, 'middle'),
      build='Puutikkaat nojaamassa hyllyyn (kallistus 12°)', collider=False),
    E('circle', 'Magic_Circle', UUSI, (0, 0.02, 18), r=5, color='#c89bff', label=None, size=(10, 0.02, 10),
      build='Lattiadecal/litteä levy, emissive violetti #b06aff, riimurengas + pentagrammi', collider=False),
    light('Circle_Glow', UUSI, (0, 1.2, 18), '#c070ff', 10, 2.0, note='Sykkii 0.6 Hz; shadows None', draw=False),
    E('fx', 'Floating_Books', UUSI, (0, 3.5, 18), draw=False,
      build='14 kirjaa (0.4 × 0.1 × 0.3) kiertää r 3–7 m, y 2–5 m; kevyt bob-skripti tai Animator; kipinät #d8a8ff (40 partikkelia)',
      collider=False),
] + [
    E('prop', f'Candle_Cluster_{i+1}', UUSI, (x, 0, z), (0.8, 0.4, 0.8), group='Candle_Cluster',
      build='3–5 kynttilää, emissive, ei valoa', collider=False)
    for i, (x, z) in enumerate(((-6.5, 13), (6.5, 13), (-6.5, 23), (6.5, 23)))
] + [
    E('text', 'label_circle', UUSI, (6, 0, 23.8), label='Loitsupiiri', color='#c89bff', anchor='start', draw_only=True),
]
z4['local'] = [
    'Laukaisualue on paikallisessa masterissa seinästä seinään, joten Malakorin ohi ei pääse kiertämällä keskushalliin.',
    'Ruudukko on paikallisessa masterissa 20 × 20 (32 m) ja liukuu sankarin luo.',
    'Dialogi-NPC seisoo pomon paikalla ja dialogi alkaa automaattisesti laukaisualueella.',
    'Käytä näissä paikallisen masterin arvoja.',
]
z4['gameplay'] = [
    'Pidä sisääntulokäytävät (z -5…5) molemmista ovista keskelle vapaina.',
    'Loitsupiiri, kynttilät ja leijuvat kirjat ovat ilman collideria, joten ne eivät estä ruutuja.',
    'Jos laukaisualue kattaa sisääntulot, dialogi alkaa heti saapuessa. Se on paikallisen masterin ratkaisu, ei tämän piirroksen.',
]
z4['mood'] = dict(
    default='A · Loitsupiiri + B:n ikkuna',
    items=[
        ('A · Loitsupiiri (oletus)', [
            'Directional: #5a4a8a, intensiteetti 0.35 (sisätila, lähes pois).',
            'Ambient: Flat #22163a.',
            'Sumu: Exponential Squared #1a1030, tiheys 0.012.',
            'Nykyiset siniset ja violetit Arcane-valot jäävät. Loitsupiirin hehku #c070ff sykkii.',
            'Leijuvat kirjat ja kipinät piirin yllä.',
        ]),
        ('B · Kuunvalo (lisänä)', [
            'Lasimaalausikkuna emissive; Window_Moon_Spot (#9ab8ff, int 2.0) heittää valojuovan lattialle.',
            'Lukupöydän kynttilät (#ffbf6a) lämpimänä vastavärinä. Pölyhiukkaset #c8d8ff valojuovassa.',
        ]),
    ])
z4['plan_notes'] = [
    'Sisään lännestä (salareitti) tai idästä (keskushalli). Ovet lukittuvat taistelun ajaksi.',
    'Loitsupiiri Malakorin alle; leijuvat kirjat piirin yllä.',
    'Hyllykäytävät kulmiin ja lukupöytä etelään; lasimaalausikkuna pohjoisseinään.',
    'Laukaisualue on paikallisessa masterissa seinästä seinään ja ruudukko 20 × 20.',
]
ZONES.append(z4)

# --------------------------------------------------------------------- Zone 5: Castle Hall
z5 = dict(
    key='05_castle_hall', num=5, scene='Zone_5_CastleHall', fi='Keskushalli', en='Castle Hall',
    extent=(80, 80), center=(0, 0), step=5,
    subtitle='Castle Hall · 80 × 80 m · turva-alue, ei taisteluja',
    concepts=['05a_hall_altar', '05b_hall_dawn'],
    summary='Turva-alue ja solmukohta: neljä ovea neljään alueeseen ja Ruunikivialttari keskellä. Pilaririvit ja matto '
            'johtavat kruununsalin porttiin. Länsiseinän ikkunoista tulee aamuvalo; juhlapöytä, lepopaikka ja takka.',
    directional=dict(color=rgb(1, 0.88, 0.65), intensity=1.25, rot=(50, -30, 0)),
    player_start=(0, 0.5, -32),
)
z5['elements'] = [
    E('floor', 'CentralHall_Floor', NYK, (0, -0.5, 0), (80, 1, 80), build='Cube, M_Ruined_walls.mat'),
    E('wall', 'Hall_Wall_South_L', NYK, (-24, 4, -40), (32, 8, 2), build='Cube'),
    E('wall', 'Hall_Wall_South_R', NYK, (24, 4, -40), (32, 8, 2), build='Cube'),
    E('wall', 'Hall_Wall_North_L', NYK, (-24, 4, 40), (32, 8, 2), build='Cube'),
    E('wall', 'Hall_Wall_North_R', NYK, (24, 4, 40), (32, 8, 2), build='Cube'),
    E('wall', 'Hall_Wall_West_S', NYK, (-40, 4, -24), (2, 8, 32), build='Cube'),
    E('wall', 'Hall_Wall_West_N', NYK, (-40, 4, 24), (2, 8, 32), build='Cube'),
    E('wall', 'Hall_Wall_East_S', NYK, (40, 4, -24), (2, 8, 32), build='Cube'),
    E('wall', 'Hall_Wall_East_N', NYK, (40, 4, 24), (2, 8, 32), build='Cube'),
] + pillars('Hall_Pillar', 35, 35, 8) + pillars('Hall_Inner_Col', 18, 18, 8) + [
    light('SanctuaryLight_Center', NYK, (0, 7.5, 0), rgb(1, 0.90, 0.60), 35, 3.0, note='shadows Soft'),
    light('SanctuaryLight_South', NYK, (0, 6, -20), rgb(1, 0.80, 0.50), 24, 2.0, note='shadows Soft'),
    light('SanctuaryLight_North', NYK, (0, 6, 20), rgb(1, 0.80, 0.50), 24, 2.0, note='shadows Soft'),
    E('shrine', 'Rune_Save_Shrine', NYK, (0, 0, 0), (3, 2.5, 3), label='Ruunikivialttari (SavePoint)', side='r',
      build='Altar.prefab + SavePoint + BoxCollider (3, 2.5, 3) center y 1.2; Rune_Crystal (0, 1.4, 0) 0.6 × 1.2 × 0.6 rot 45; '
      'ShrineGlow (0, 2.2, 0) syaani (0.2, 0.9, 1.0) range 14 int 2.5', collider=True),
    spawn('Spawn_From_Courtyard', (0, 0.5, -34), 0, side='l'),
    spawn('Spawn_From_Library', (-34, 0.5, 0), 90, side='b'),
    spawn('Spawn_From_Tower', (34, 0.5, 0), -90, side='b'),
    spawn('Spawn_From_ThroneRoom', (0, 0.5, 34), 180),
    door('Door_Hall_Courtyard', (0, 0, -39.5), 180, 'Zone_3_CastleCourtyard', 'Spawn_From_Hall', 8, 16,
         'Return to Courtyard (Wing 1)', '↓ Linnan alapiha', 'x', -1),
    door('Door_Hall_Library', (-39.5, 0, 0), -90, 'Zone_4_Library', 'Spawn_From_Hall', 8, 16,
         'Enter Arcane Library (Wing 2)', 'Kirjasto', 'z', -1),
    door('Door_Hall_Tower', (39.5, 0, 0), 90, 'Zone_6_Tower', 'Spawn_From_Hall', 8, 16,
         'Ascend Spiral Stairs into Treasure Tower', 'Torni', 'z', 1),
    door('Door_Hall_ThroneRoom', (0, 0, 39.5), 0, 'Zone_7_ThroneRoom', 'Spawn_From_Hall', 8, 16,
         'Confront Final Boss in Crown Hall (Wing 3)', '↑ Kruununsali (3. pomo)', 'x', 1),
    # ---------------- new
    E('prop', 'Carpet_South', UUSI, (0, 0.01, -22.5), (6, 0.02, 31), color='#ff5d5d', label='Punainen matto', lab=(3.6, -31, 'start'),
      group='Carpet', build='Litteä levy, punainen #a8242e reunus #8a1a24', collider=False),
    E('prop', 'Carpet_North', UUSI, (0, 0.01, 22.5), (6, 0.02, 31), color='#ff5d5d', group='Carpet', build='sama', collider=False),
    E('circle', 'Altar_Rune_Ring', UUSI, (0, 0.02, 0), r=7, color='#5fe1ff', size=(14, 0.02, 14), label=None,
      build='Lattiadecal, syaani #5ad8ff emissive riimurengas', collider=False),
    E('fx', 'Altar_Light_Pillar', UUSI, (0, 0, 0), draw=False,
      build='Additiivinen sylinteri r 0.8, korkeus 10 m (#aaf4ff, alpha 0.25) + nousevat hiukkaset', collider=False),
] + [
    E('pillar', f'Nave_Pillar_{"W" if x < 0 else "E"}{abs(z)}{"S" if z < 0 else "N"}', UUSI, (x, 0, z), (1.2, 8, 1.2), r=1.4, group='Nave_Pillar',
      build='Pillar.prefab, scale (1.2, 1.6, 1.2) kuten Hall_Pillar', collider=True,
      label='pilaririvi' if (z == -30 and x > 0) else None, lab=(x, -34.6, 'middle'))
    for x in (-12, 12) for z in (-30, -20, -10, 10, 20, 30)
] + [
    E('torch', f'Torch_Stand_{i+1}', UUSI, (x, 0, z), (0.5, 1.8, 0.5), group='Torch_Stand',
      build='Jalallinen soihtu + liekkipartikkeli', collider=True)
    for i, (x, z) in enumerate(((-12, -25), (12, -25), (-12, 25), (12, 25)))
] + [
    light(f'Torch_Light_{i+1}', UUSI, (x, 2.2, z), '#ffb347', 9, 1.4, note='shadows None, värinä', group='Torch_Light', draw=False)
    for i, (x, z) in enumerate(((-12, -25), (12, -25), (-12, 25), (12, 25)))
] + [
    E('banner', f'Banner_Nave_{i+1}', UUSI, (x, 5, z), (1.6, 4, 0.15), rot=(90 if x < 0 else -90), group='Banner_Nave', color='#5f8fff',
      build='Sininen viiri #2a4a9a, kultainen tunnus #e8c35a, pilarin keskilaivan puolella', collider=False)
    for i, (x, z) in enumerate(((-10.9, -30), (10.9, -30), (-10.9, 30), (10.9, 30)))
] + [
    E('prop', 'Banquet_Table', UUSI, (-28.5, 0, -18), (15, 0.9, 4), label='Juhlapöytä + penkit',
      build='Pöytä 13 × 0.9 × 2.2 + 2 penkkiä 13 × 0.5 × 0.6; ruokaa, kynttilät, kannut', collider=True),
    E('prop', 'Fireplace', UUSI, (37.5, 0, 24), (3, 5, 10), color='#ffab4a', label='Takka', lab=(35, 24, 'end'),
      build='Kivitakka, aukko länteen (-X); tulipartikkelit; viiri yläpuolella (38.8, 6, 24)', collider=True),
    light('Fireplace_Fire', UUSI, (36, 1.2, 24), '#ff9a3a', 12, 2.0, note='Värisee; shadows None', draw=False),
    E('prop', 'Rest_Area', UUSI, (-25, 0, 26), (10, 1, 6), label='Lepopaikka',
      build='Matto 10 × 6 (ei collideria), 2 makuualustaa, Barrel, Box_2, matala pöytä', collider=True),
] + [
    E('window', f'Tall_Window_{i+1}', UUSI, (-38.9, 4.5, z), (0.3, 6, 3.5), color='#ffe8b0', group='Tall_Window',
      build='Kehys + emissive lasi #ffe8b0; valojuova (additiivinen taso) ikkunasta itään', collider=False,
      label='korkeat ikkunat (aamuvalo)' if i == 0 else None, lab=(-37, -36.4, 'start'))
    for i, z in enumerate((-30, -14, 14, 30))
]
z5['local'] = []
z5['gameplay'] = [
    'Ei taisteluita. Pidä neljän oven 16 m aukot ja spawn-pisteet vapaina.',
    'Pilaririvien välissä on 20 m aukko (z -10…10), joten itä–länsi-akseli kirjastosta torniin pysyy suorana.',
    'Juhlapöydän ja Hall_Inner_Col_2:n (-18, -18) väliin jää vajaa 1 m. Jos se tuntuu ahtaalta, siirrä pöytää länteen.',
    'Alttarin SavePoint-collider (3 × 2.5 × 3) säilyy; rengas ja valopilari ovat ilman collideria.',
]
z5['mood'] = dict(
    default='Lämmin turvapaikka + aamuvalo',
    items=[
        ('Oletus: lämmin turvapaikka ja aamuvalo', [
            'Nykyiset lämpimät valot jäävät. Directional aamuauringoksi länsi-ikkunoista: #ffe0a0, int 1.3, rotaatio (30, 90, 0).',
            'Ambient: taivas #5a4a48, maa #3a3034.',
            'Soihdut #ffb347, takka #ff9a3a, alttarin syaani #5ad8ff (ShrineGlow).',
            'Pölyhiukkaset ikkunoiden valojuovissa (#fff0c8, ~80 kpl).',
        ]),
        ('Vaihtoehto: soihtuyö (konsepti A)', [
            'Directional lähes pois (#2a2228, int 0.2); soihdut ja alttari kantavat valaistuksen.',
        ]),
    ])
z5['plan_notes'] = [
    'Solmukohta: neljä ovea neljään alueeseen, alttari keskellä.',
    'Pilaririvit ja matto ohjaavat katseen kruununsalin porttiin.',
    'Juhlapöytä, lepopaikka ja takka tekevät hallista hengähdyspaikan.',
    'Ikkunat länsiseinällä, jotta aamuvalo osuu pöytään ja alttariin.',
]
ZONES.append(z5)

# ------------------------------------------------------------------------- Zone 6: Tower
z6 = dict(
    key='06_tower', num=6, scene='Zone_6_Tower', fi='Aarretorni', en='Tower',
    extent=(30, 30), center=(0, 0), step=2.5,
    subtitle='Tower · 30 × 30 m kahdeksankulmio · ei taisteluja',
    concepts=['06b_tower_treasure', '06a_tower_exterior'],
    summary='Pieni kahdeksankulmainen aarrekammio, yksi ovi keskushalliin. Kattoikkunan valokeila osuu keskijalustaan, '
            'jolla ovat Othelian sormus ja Jättiläisen eliksiiri. Kultakasat pohjoisessa, juomahyllyt sivuilla.',
    directional=dict(color=rgb(0.45, 0.65, 1.0), intensity=1.1, rot=(50, -30, 0)),
    player_start=(0, 0.5, -9),
)
z6['elements'] = [
    E('floor', 'Tower_Floor', NYK, (0, -0.5, 0), (30, 1, 30), build='Cube, M_Ruined_walls.mat'),
    E('wall', 'Tower_Wall_North', NYK, (0, 4, 15), (16, 8, 2), build='Cube'),
    E('wall', 'Tower_Wall_East', NYK, (15, 4, 0), (2, 8, 16), build='Cube'),
    E('wall', 'Tower_Wall_West', NYK, (-15, 4, 0), (2, 8, 16), build='Cube'),
    E('wall', 'Tower_Wall_South_L', NYK, (-10, 4, -15), (10, 8, 2), build='Cube'),
    E('wall', 'Tower_Wall_South_R', NYK, (10, 4, -15), (10, 8, 2), build='Cube'),
    E('wall', 'Wall_NE', KORJ, (10.6, 4, 10.6), (2, 8, 10), rot=-45, was_rot=45, group='Wall_diag',
      build='Cube', desc='Koodissa rot 45, joka kääntää seinän säteittäiseksi: sen kummallekin puolelle jää noin 3 m rako lattian reunalle. Oikea arvo -45.'),
    E('wall', 'Wall_NW', KORJ, (-10.6, 4, 10.6), (2, 8, 10), rot=45, was_rot=-45, group='Wall_diag', build='Cube',
      desc='Koodissa -45, oikea 45.'),
    E('wall', 'Wall_SE', KORJ, (10.6, 4, -10.6), (2, 8, 10), rot=45, was_rot=-45, group='Wall_diag', build='Cube',
      desc='Koodissa -45, oikea 45.'),
    E('wall', 'Wall_SW', KORJ, (-10.6, 4, -10.6), (2, 8, 10), rot=-45, was_rot=45, group='Wall_diag', build='Cube',
      desc='Koodissa 45, oikea -45.'),
    light('TowerLight_Center', NYK, (0, 6.5, 0), rgb(0.55, 0.85, 1.0), 24, 2.5, note='shadows Soft; oletustunnelmassa int → 1.0'),
    spawn('Spawn_From_Hall', (0, 0.5, -9), 0),
    door('Door_Tower_CastleHall', (0, 0, -14.5), 180, 'Zone_5_CastleHall', 'Spawn_From_Tower', 6, 10,
         'Return to Great Central Hall', '↓ Keskushalli', 'x', -1),
    E('chest', 'Tower_Reward_Chest', NYK, (-5, 0.4, 5), label='Arkku (50 g)', side='b',
      build='Chest.prefab + ChestRewardInteraction 50 g + Item_SignetRing (oletuksessa sormus siirtyy jalustalle, arkkuun jää 50 g)'),
    E('pedestal', 'GiantElixir_Pedestal', KORJ, (0, 0.5, 4), (1.5, 1, 1.5), was_pos=(5, 0.5, 5),
      label='Jalusta: eliksiiri + sormus', side='b',
      build='Cube M_Rocks_1_2 + GiantElixirInteraction, collider 2.5 × 2 × 2.5; Elixir_Vial (0, 0.8, 0); ElixirGlow (0, 1.5, 0) range 8 int 2',
      desc='Nykyään (5, 0.5, 5). Piirroksen mukaan valokeilan keskelle (0, 0.5, 4).'),
    # ---------------- new
    E('pickup', 'SignetRing_Pickup', UUSI, (0, 1.15, 4.5),
      build='Sormusmalli jalustalla + ChestRewardInteraction (gold 0, Item_SignetRing), kuten suoyrtit',
      desc='Sormus pois arkusta (ItemReward → null). Jos tämä tuntuu liian isolta muutokselta, jätä sormus arkkuun.'),
    E('circle', 'Skylight_Beam', UUSI, (0, 0, 4), r=2.6, color='#fff0c0', size=(5.2, 9.5, 5.2), label='Valokeila kattoikkunasta',
      lab=(0, 7.6, 'middle'), build='Additiivinen kartio r 2.6 + Spot alla', collider=False),
    light('Skylight_Spot', UUSI, (0, 9.5, 4), '#fff0c0', 14, 3.0, ltype='spot', note='rot (90, 0, 0) suoraan alas, kulma 35°, shadows None', draw=False),
    E('region', 'Gold_Piles', UUSI, (0, 0, 11), pts=[(-8, 11), (-3, 13.4), (3, 13.4), (8, 11), (4, 9.5), (-4, 9.5)],
      color='#ffd24a', label='Kultakasat', lab=(0, 11.5, 'middle'),
      build='Matala kultakasa (0.6–1.0 m) + yksittäisiä kolikoita; yhdistä yhdeksi staattiseksi meshiksi, ei Pickup-skriptejä',
      collider=False),
    light('Gold_Glow', UUSI, (0, 1.5, 11), '#ffcc4a', 8, 1.2, note='shadows None', draw=False),
    E('prop', 'Potion_Shelf_W', UUSI, (-13.4, 0, 2), (1.2, 3, 8), label='juomahylly', lab=(-12.2, 7, 'start'), group='Potion_Shelf',
      build='Hylly 2 tasoa × 6 pulloa, emissive (#d83a3a, #3a8ad8, #6ad83a, #c83ad8)', collider=True),
    E('prop', 'Potion_Shelf_E', UUSI, (13.4, 0, 2), (1.2, 3, 8), label='juomahylly', lab=(12.2, 7, 'end'), group='Potion_Shelf',
      build='sama', collider=True),
    E('circle', 'Spiral_Stairs', UUSI, (-6.5, 0, -6.5), r=2.2, color='#8ff0a0', size=(4.4, 6, 4.4), label='kierreportaat',
      lab=(-6.5, -2.9, 'middle'), build='Stairs.prefab-kierre tai kuutioaskelmat, johtaa ylös (koriste)', collider=True),
]
z6['local'] = []
z6['gameplay'] = [
    'Tarkista ensin kulmat: jos lattian kulmissa on aukot (koodin rotaatioilla on), korjaa vinoseinien rot (NE -45, NW 45, SE 45, SW -45).',
    'Kulkureitti ovelta (0, -14.5) jalustalle pysyy suorana; kierreportaat ovat sivussa.',
    'Jos sormus siirretään jalustalle, tarkista, ettei Othelian tehtävä oleta sormuksen tulevan arkusta.',
]
z6['mood'] = dict(
    default='B · Aarrekammio',
    items=[
        ('B · Aarrekammio (oletus)', [
            'Skylight_Spot (#fff0c0, int 3) valokeilana jalustalle; TowerLight_Center int 2.5 → 1.0 (viileä täyte).',
            'Kultakasat emissive + Gold_Glow (#ffcc4a). Juomapullot emissive.',
            'Eliksiiri: konseptissa punainen lasi #e02a4a ja hehku #ff3a5a (nyt kulta). Valinnainen.',
            'Pölyhiukkaset valokeilassa (#fff0c0, ~90 kpl).',
        ]),
        ('A · Torni ulkoa hämärässä', [
            'Ei rakennettavissa tähän sceneen (sisätila). Käytä retkikartan kuvakkeena tai latausruutuna.',
        ]),
    ])
z6['plan_notes'] = [
    'Kahdeksankulmio, yksi ovi keskushalliin.',
    'Vinoseinien rotaatio korjattu: koodin arvoilla kulmiin jää aukot.',
    'Eliksiiri ja sormus samalle jalustalle valokeilaan; arkkuun jää 50 g.',
    'Kultakasat pohjoiseen, juomahyllyt sivuseinille.',
]
ZONES.append(z6)

# -------------------------------------------------------------------- Zone 7: Throne Room
z7 = dict(
    key='07_throne_room', num=7, scene='Zone_7_ThroneRoom', fi='Kruununsali', en='Throne Room',
    extent=(100, 100), center=(0, 0), step=5,
    subtitle='Throne Room · 100 × 100 m · 3. pomo: Kivettymiskuningas',
    concepts=['07a_throne_petrify', '07b_throne_golem'],
    summary='Pitkä sali etelästä valtaistuimelle. Matto ja kivettyneet ritarit rakentavat jännitettä. '
            'Ensimmäisessä vaiheessa kylmä turkoosi valo, golemin toisessa vaiheessa lattia halkeilee laavana.',
    directional=dict(color=rgb(1, 0.40, 0.20), intensity=1.3, rot=(50, -30, 0)),
    player_start=(0, 0.5, -42),
)
z7['elements'] = [
    E('floor', 'ThroneRoom_Floor', NYK, (0, -0.5, 0), (100, 1, 100), build='Cube, M_Ruined_walls.mat'),
    E('wall', 'ThroneRoom_Wall_North', NYK, (0, 4.5, 50), (100, 9, 2), build='Cube'),
    E('wall', 'ThroneRoom_Wall_West', NYK, (-50, 4.5, 0), (2, 9, 100), build='Cube'),
    E('wall', 'ThroneRoom_Wall_East', NYK, (50, 4.5, 0), (2, 9, 100), build='Cube'),
    E('wall', 'ThroneRoom_Wall_South_L', NYK, (-28, 4.5, -50), (44, 9, 2), build='Cube'),
    E('wall', 'ThroneRoom_Wall_South_R', NYK, (28, 4.5, -50), (44, 9, 2), build='Cube'),
    E('dais', 'Throne_Dais', NYK, (0, 0.6, 32), (16, 1.2, 10), label='Valtaistuinkoroke', lab=(-9, 29.5, 'end'), build='Cube'),
    E('wall', 'Throne_Col_L', NYK, (-7, 4.5, 32), (2, 9, 2), build='Cube'),
    E('wall', 'Throne_Col_R', NYK, (7, 4.5, 32), (2, 9, 2), build='Cube'),
] + pillars('Throne_Pillar', 45, 45, 9) + [
    light('CrownLight_Center', NYK, (0, 7.5, 10), rgb(1, 0.40, 0.15), 35, 3.0, note='shadows Soft'),
    light('CrownLight_Throne', NYK, (0, 6, 32), rgb(1, 0.85, 0.25), 28, 2.5, note='shadows Soft'),
    spawn('Spawn_From_Hall', (0, 0.5, -42), 0),
    door('Door_ThroneRoom_CastleHall', (0, 0, -49.5), 180, 'Zone_5_CastleHall', 'Spawn_From_ThroneRoom', 8, 12,
         'Return to Great Central Hall (Hub)', '↓ Keskushalli', 'x', -1),
    E('barrier', 'Barrier_South', NYK, (0, 4, -49), (12, 8, 1.5), build='Cube M_HammerJAanvil, näkyy taistelun ajan'),
    E('grid', 'CombatGrid_CrownHall', NYK, (0, 0.05, 12), tiles=12, tile=1.6, build='CombatGrid.prefab',
      desc='Paikallisessa masterissa 20 × 20 ja liukuu sankarin luo.'),
    E('boss', 'Boss_GargoyleKing', NYK, (0, 1.8, 28), label='Kivettymiskuningas / golem', side='r',
      build='GargoyleKingBoss (golem vaihtaa 2. vaiheen malliin puolessa HP:ssa)'),
    E('npc', 'NPC_GargoyleKing', PAIK, (0, 1.2, -10), label='dialogi-NPC (paik. masterissa pomon paikalla)', side='b',
      build='VillageNPC + GargoyleKing_Intro.asset'),
    E('chest', 'CrownHall_Treasure_Chest', NYK, (0, 1.4, 34), label='Arkku (100 g)', side='r',
      build='Chest.prefab 100 g; näkyy voiton jälkeen'),
    E('trigger', 'CrownHall_Encounter_Trigger', NYK, (0, 2.5, 15), (36, 6, 36),
      build='BoxCollider trigger + DungeonRoomController (GargoyleKing)',
      desc='Paikallisessa masterissa seinästä seinään (tarkka z-väli siellä).',
      label='laukaisualue (paik. masterissa seinästä seinään)'),
    # ---------------- new
    E('prop', 'Carpet', UUSI, (0, 0.01, -10.5), (6, 0.02, 75), color='#ff5d5d', label='Matto', lab=(3.8, -30, 'start'),
      build='Litteä levy, punainen; ovelta korokkeen eteen (z -48…27)', collider=False),
] + [
    E('circle', f'Petrified_Knight_{"W" if x < 0 else "E"}{i+1}', UUSI, (x, 0, z), r=1.6, color='#c8d0d0',
      rot=(90 if x < 0 else -90), size=(2, 3.7, 2), group='Petrified_Knight',
      build='Jalusta 2 × 0.5 × 2 + harmaa kivipatsas 1.2 × 3.2 × 1.2 (#8a9290), vaihtelevat asennot (kilpi/kypärä tai huppu)',
      collider=True, label='kivettyneet ritarit' if i == 0 else None, lab=(x, -33, 'middle'))
    for x in (-14, 14) for i, z in enumerate((-38, -28, -18, -8))
] + [
    E('pillar', f'Hall_Column_{"W" if x < 0 else "E"}{i+1}', UUSI, (x, 0, z), (1.6, 9, 1.6), r=2.0, group='Hall_Column',
      build='Pillar.prefab, scale (1.6, 1.8, 1.6)', collider=True,
      label='pilarit' if i == 0 else None, lab=(x, -45.5, 'middle'))
    for x in (-30, 30) for i, z in enumerate((-40, -25, -10, 5, 20, 35))
] + [
    E('prop', 'Throne', UUSI, (0, 1.2, 35.6), (4, 5, 2.2), rot=180, label='Valtaistuin', lab=(0, 39.6, 'middle'),
      build='Korkeaselkäinen kivivaltaistuin korokkeen takaosassa, kasvot etelään; 2. vaiheessa rikottu versio (valinnainen)',
      collider=True),
    E('crack', 'Lava_Cracks', UUSI, (0, 0.02, 12),
      polylines=[[(-6, 6), (-3, 9), (-4, 13), (0, 16)], [(5, 4), (8, 8), (6, 12)], [(-8, 16), (-4, 19), (2, 18)], [(3, 14), (7, 17), (9, 20)]],
      label='laavahalkeamat (2. vaihe)', lab=(-10.5, 12.5, 'end'),
      build='Emissive-halkeamadecalit (#ff6a1a, hehku #ff9a3a) 0.3–0.5 m leveitä; piilossa 1. vaiheessa, päälle kun golem vaihtaa 2. vaiheen malliin',
      collider=False),
    E('fx', 'Phase2_Switch', UUSI, (0, 0, 0), draw=False,
      build='Golemin vaihdon hetkellä: Lava_Cracks päälle, valot 2. vaiheen paletille, kipinäpartikkelit (#ffa04a, 60 kpl), lyhyt kameran tärähdys',
      collider=False),
]
z7['local'] = [
    'Laukaisualue on paikallisessa masterissa seinästä seinään, ja kuninkaan dialogi alkaa aina ennen valtaistuinta.',
    'Ruudukko on paikallisessa masterissa 20 × 20 (32 m) ja liukuu sankarin luo.',
    'Dialogi-NPC seisoo pomon paikalla. Käytä näissä paikallisen masterin arvoja.',
]
z7['gameplay'] = [
    'Pomo seisoo korokkeella (0, 28). Paikallisessa masterissa ruudukko liukuu, joten tarkista, että pomo on ruudukolla taistelun alkaessa.',
    'Laavahalkeamat ja matto ovat ilman collideria, joten ne eivät estä ruutuja.',
    'Ritarit ja pilarit ovat collidereineen suojia, jos ruudukko liukuu niiden päälle. Keskikäytävä (x -8…8) pysyy vapaana.',
    'Ainoa ovi on etelässä ja se lukittuu taistelun ajaksi.',
]
z7['mood'] = dict(
    default='1. vaihe A, 2. vaihe B',
    items=[
        ('1. vaihe · Kivettynyt hovi (A)', [
            'Directional: #9fc8c0, int 0.8. Ambient: taivas #2a3436, maa #1a2224.',
            'Sumu: Linear #9ac8c0, 30–120 m.',
            'CrownLight_Throne → turkoosi #6af0d0. Kuninkaan silmät #ffe24a. Pölyhiukkaset #d8fff4.',
        ]),
        ('2. vaihe · Laavahehku (B), golemin vaihdon hetkellä', [
            'Lava_Cracks päälle (#ff6a1a / #ff9a3a). CrownLight_Center → #ff5a1a int 3.',
            'Directional takaisin nykyiseen punaoranssiin (1, 0.40, 0.20) int 1.3.',
            'Kipinäpartikkelit #ffa04a (60 kpl), lyhyt kameran tärähdys.',
        ]),
    ])
z7['plan_notes'] = [
    'Pitkä sisääntulo: matto, kivettyneet ritarit ja pilarit ennen pomoa.',
    'Valtaistuin korokkeen takaosassa; arkku sen edessä (näkyy voiton jälkeen).',
    'Laavahalkeamat näkyvät vasta golemin 2. vaiheessa.',
    'Laukaisualue on paikallisessa masterissa seinästä seinään ja ruudukko 20 × 20.',
]
ZONES.append(z7)
