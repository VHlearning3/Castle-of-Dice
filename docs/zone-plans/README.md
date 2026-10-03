# Alueiden rakennusohjeet: Zone 2–7

Vilin pyyntö 3.10.2026: *"rakenna kaikki pelin alueet paitsi kylä joka jo valmis piirrustusten mukaisesti"*.

Tässä kansiossa ovat pohjapiirrokset ja tarkat rakennusohjeet kuudelle alueelle: Metsäpolku, Linnan alapiha, Kirjasto, Keskushalli, Aarretorni ja Kruununsali. **Kylään (Zone 1) ei kosketa.** Se on käsin muokattu, eikä sitä saa rakentaa uudelleen työkalulla.

Taulukot, piirrokset ja JSON tehdään samasta datatiedostosta (`tools/zone_data.py`), joten niiden luvut täsmäävät keskenään.

## Tiedostot

| Tiedosto | Sisältö |
|---|---|
| `02_forest_path.md` … `07_throne_room.md` | Alueen kaikki elementit: sijainti, koko, rotaatio, collider ja rakennusohje. Lisäksi valaistus, tunnelma ja pelilliset huomiot. |
| `plans/*.jpg` | Pohjapiirrokset (1600 × 1000, JPG, koska repon `.gitattributes` vie PNG:t Git LFS:ään). Valkoinen = nykyinen, vihreä katkoviiva = uusi, oranssi katkoviiva = korjaus, keltainen katkoviiva = paikallisen masterin arvo. |
| `concepts/*.jpg` | Konseptikuvat, kaksi per alue (tunnelmavaihtoehdot A ja B). |
| `zone-plans.json` | Sama data koneluettavana (alueet → elements[] kentillä `id`, `kind`, `status`, `pos`, `size`, `rot` …). |
| `tools/` | `zone_data.py` (data), `zone_build.py` (tekee piirrokset, taulukot ja JSONin), `render_svg.js` (SVG → PNG). |

## Ennen kuin aloitat

1. **Pohja:** käytä haaraa `claude/commander-door-grid` tai paikallista masteria sen jälkeen, kun se on yhdistetty. Siinä laukaisualueet, taisteluruudukot, pomojen dialogit ja dialogi-NPC:t on jo muutettu Vilin pyynnöstä. Tämä paketti ei muuta niitä.
2. **Rakenna builderiin.** `ZoneSceneBuilder.cs` (menu *CastleOfDice/Build Zone Scenes*) luo alueet 2–7 tyhjästä (`EditorSceneManager.NewScene`). Lisää uudet objektit builderiin, esimerkiksi omaan `DressZoneN`-metodiin per alue. Skeneen käsin tehty muutos katoaa seuraavassa rakennuksessa.
3. **Rakennusjärjestys:** ensin *Build Zone Scenes*, sitten *CastleOfDice/Setup Rigged Enemies, Bosses & Heroes*. Jälkimmäinen pukee pomot ja NPC:t malleihin (Zone 3, 4 ja 7) ja lisää Metsäpolun zombitaistelun (`DressZone2`). `StandOnGround` laskee yksiköt maahan säteellä, joten älä sijoita uusia esineitä yksiköiden alle.

## Koordinaatit ja merkinnät

- **Koordinaatit:** Unityn maailmakoordinaatit metreinä. X = itä (piirroksessa oikealle), Z = pohjoinen (ylös) ja Y = ylös.
- **Rot Y:** `Quaternion.Euler(0, rot, 0)`. Positiivinen kääntää myötäpäivään ylhäältä katsottuna, eli rot 90 osoittaa +X:ään (itään).
- **Koko:** paikallinen skaala ennen rotaatiota (kuten `localScale`). Esimerkiksi koko 2 × 8 × 10 ja rot -45 tarkoittaa 10 m pitkää seinää, jonka pitkä akseli kulkee luoteesta kaakkoon.
- **Y-arvo:**
  - Nykyisissä riveissä y on täsmälleen sama kuin builderissa. `CreateCube` käyttää kappaleen keskipistettä.
  - Uusissa esineissä y on taso, jolla esine seisoo: 0 = lattia, 1.2 = valtaistuinkorokkeen päällä. Kuutiona rakennettaessa keskipiste on silloin y + korkeus / 2.
  - Seinäviireissä ja ikkunoissa y on keskipiste.
  - Valoissa y on valon paikka.
- **Pyöreät esineet:** r tarkoittaa sädettä metreinä.
- **Ruudukko:** taisteluruutu on 1.6 m. 12 × 12 ruudukko on siis 19.2 m.

## Tilat

| Tila | Merkitys | Mitä teet |
|---|---|---|
| **nykyinen** | Objekti on GitHub masterissa (`ZoneSceneBuilder.cs` tai `SetupRiggedEnemiesEditor.cs`). | Pidä ennallaan. |
| **uusi** | Konseptikuvista tuleva lisäys. | Rakenna. |
| **korjaus** | Olemassa oleva objekti, jonka arvo muuttuu. Taulukossa uusi arvo lihavoituna ja nykyinen suluissa. | Muuta arvo. |
| **paikallinen** | Arvoa hallitsee Vilin koneen haara `claude/commander-door-grid` (paikallinen master). | Älä muuta tämän paketin mukaan. |

**Etusija:** Vilin koneen versio voittaa kaikissa pelillisissä objekteissa: laukaisualueet, ruudukot, pomot, viholliset, dialogi-NPC:t, ovet, spawnit, barrierit, arkut ja `DungeonRoomController`. Jos jokin arvo poikkeaa tästä paketista, käytä koneen arvoa ja rakenna ympäristö sen ympärille.

## Yleiset rakennussäännöt

1. **Kiinteät esineet** (seinät, tornit, hyllyt, pilarit, pöydät, kivet, puut) saavat colliderin ja Layer 7 `Obstacle`. Polut, matot, decalit, valokeilat, sade ja partikkelit eivät saa collideria.
2. **Taisteluruudukko:** `GridManager.cs` (rivit 244–258) merkitsee ruudun kulkukelvottomaksi, jos `Physics.OverlapBox` osuu ruudun kohdalla ei-trigger-collideriin. Laatikko on 0.1–1.9 m korkeudella ja noin 1.1 × 1.1 m kokoinen (0.7 × ruutu), eikä siinä ole layer-maskia. Taistelualueelle jäävä kiinteä esine on siis suoja.
   - GitHub masterin 12 × 12 -ruudukoiden alueella ei ole uusia kiinteitä esineitä.
   - Paikallisen haaran isommat ruudukot (32 × 32 alapihalla, 20 × 20 kirjastossa ja kruununsalissa) ulottuvat osaan niistä. Alapihan hiillokset ovat silloin tarkoituksella 1 ruudun suojia. Reunojen esineet ovat vain kulkukelvottomia ruutuja.
3. **Ovet ja spawnit:** pidä ovien edustat ja spawn-pisteiden ympäristö (vähintään 3 m) vapaina. `DoorTeleporter`-triggereitä, barriereita ja spawn-nimiä ei muuteta.
4. **Kamera:** seurantakamera on offsetissa (-6, 12, -12) eli lounaassa sankarista (`ZoneSceneBuilder.cs:769`). Korkeat esineet sankarin etelä- ja länsipuolella voivat peittää näkymän. Pidä kulkureittien varrella olevat esineet alle noin 4 m korkeina, tai sijoita ne reunoille.
5. **WebGL (60 FPS):**
   - Uusille valoille shadows None. Nykyiset Soft-varjolliset valot jäävät ennalleen.
   - Hehkut tehdään emissive-materiaaleilla.
   - Liikkumattomat koristeet merkitään Staticiksi (static batching).
   - Partikkelit pidetään kohtuullisina, korkeintaan noin 600 kerrallaan.
   - Ei `Instantiate`/`Destroy`-silmukoita.
   - Äänet soitetaan `SFXManager`-poolin kautta.
6. **Vihollisia ei lisätä.** Sääntö: taistelussa 1 eliitti tai enintään 2 vihollista, ja pomolla enintään 1 apulainen.
7. **Materiaalit ja mallit:** käytä projektin low-poly-prefabeja ja materiaaleja. Uudet värit on annettu hex-arvoina.

Käytettävissä olevat prefabit (`Assets/PREFABS`):
- **Luonto:** Pine_tree, Pine_tree_2, Tree_1/2/4, Bush_*, Fern, Flowers_*, Grass_*, Leaves_*, Mushroom_1–4, Rock_1–4, Log*, Stump
- **Rakennukset:** Wall_1/2, Pillar, Pillar_ruined_1/2, Arch, Arch_part, Tower, Stairs, Stairs_Large, Stone_fence, Fence*, Tile_1–7
- **Esineet:** Altar, Barrel, Box_1–4, Cauldron, Chest, Crystal_1–5, Lamppost, street_light, Well

## Tärkeät löydökset

**Aarretornin kulmissa on aukot (Zone 6, korjaus).** `ZoneSceneBuilder.cs` rivit 608–615 asettavat vinoseinille arvot NE 45, NW -45, SE -45 ja SW 45. Seinän pitkä akseli on paikallinen Z (10 m), ja `Euler(0, 45, 0)` kääntää sen koilliseen. Silloin NE-seinä osoittaa kulmaan päin eikä sulje sitä, ja seinän kummallekin puolelle jää noin 3 m levyinen rako, josta pääsee lattian reunalle. Sama toistuu kaikissa neljässä kulmassa. Oikeat arvot ovat NE -45, NW 45, SE 45 ja SW -45. Tämä on päätelty koodista, ei pelattu. Tarkista se ensin Unityssä.

**Aarretornin jalusta (Zone 6, korjaus).** Eliksiirijalusta siirtyy kohdasta (5, 0.5, 5) valokeilan keskelle (0, 0.5, 4), ja Othelian sormus nostetaan arkusta samalle jalustalle. Arkkuun jää 50 g. Jos sormuksen siirto tuntuu liian isolta muutokselta, jätä sormus arkkuun.

**Erot gallerian aiempiin piirroksiin.** Nämä piirrokset korvaavat ne.
- Aiemmissa piirroksissa rotaatiot oli piirretty väärään suuntaan, joten tornin kulmat näyttivät umpinaisilta.
- Kulmapilarit (`CreateCornerPillars`) puuttuivat. Nyt ne ovat mukana.
- Metsäpolulta puuttui nykyinen zombitaistelu (ruudukko (0, -24), zombi (0, -18), laukaisualue 20 × 14 m). Nyt se on mukana, ja kivet ja mäntyryhmä on siirretty sen ruudukon ulkopuolelle.
- Metsäpolun piirroksessa oli ylimääräinen valo kohdassa (-28, -40). Se on poistettu.
- Metsäpolun salaportin taakse ehdotettu pensasalue on poistettu. Kirjastosta palaava sankari ilmestyy kohtaan (-28, 15), ja hänen on päästävä sieltä pois ilman tiirikointia.
- Keskushallin juhlapöytä on siirretty kohtaan x -28.5, koska se osui pilariin (-18, -18).
- Kruununsalin valtaistuin on nyt korokkeen päällä takaosassa (0, 1.2, 35.6). Aiemmin se oli korokkeen takana (z 40).

## Oletustunnelmat

| Alue | Oletus | Konseptikuvat |
|---|---|---|
| Zone 2 Metsäpolku | A · Päivä (B · Kuutamoyö vaihtoehtona) | `02a`, `02b` |
| Zone 3 Linnan alapiha | A · Myrsky (sade ja salamat) | `03a`, `03b` |
| Zone 4 Kirjasto | A · Loitsupiiri + B:n kuunvaloikkuna | `04a`, `04b` |
| Zone 5 Keskushalli | Lämmin turvapaikka + aamuvalo ikkunoista | `05a`, `05b` |
| Zone 6 Aarretorni | B · Aarrekammio (valokeila jalustalle) | `06b` (`06a` on ulkokuva) |
| Zone 7 Kruununsali | 1. vaihe A (kivettynyt hovi), 2. vaihe B (laavahehku golemin vaihtuessa) | `07a`, `07b` |

Tarkat värit, intensiteetit ja sumuarvot ovat kunkin alueen tiedostossa kohdassa *Valaistus ja tunnelma*.

## Haku Vilin koneelle

Koneen remote on nimeltään `Origin`:

```
git fetch Origin claude/project-thread-e668p4
git show Origin/claude/project-thread-e668p4:docs/zone-plans/README.md
git worktree add D:\Unity\castle-zone-plans Origin/claude/project-thread-e668p4
```

Haarassa on vain tämä `docs/zone-plans/`-kansio GitHub masterin päällä, ei pelikoodia.

## Piirrosten päivitys

Muuta arvot tiedostoon `tools/zone_data.py` ja aja:

```
python3 tools/zone_build.py out
node tools/render_svg.js out/plans_svg out/plans
for f in out/plans/*.png; do convert "$f" -quality 92 -sampling-factor 4:4:4 -strip "plans/$(basename "$f" .png).jpg"; done
```

`render_svg.js` tarvitsee Playwrightin ja Chromiumin. Muunnos JPG:ksi tehdään ImageMagickilla.
