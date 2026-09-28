# 🎲 CASTLE OF DICE — MASTER AGENT & ARCHITECTURE DIRECTIVES (v2.6)

Tämä tiedosto on Antigravity- ja Claude Code -agenttien ylin ohjesääntö ja totuuden lähde (Source of Truth).
Kohdeympäristö: **Unity 6 (6000.3.14f1 LTS) WebGL (60 FPS)** | C# (.NET Standard) | URP Low-Poly.

---

## 1. KNOWLEDGE BASE & GROUNDING (Strict NotebookLM Policy)

- Kaikkien alagenttien ja orkestraattorin AINOA sallittu ulkoinen tietolähde on:
  - **Notebook ID:** `ab6acec3-5413-4df2-8352-5fd173af3806`
  - **Resource Name:** `notebooks/ab6acec3-5413-4df2-8352-5fd173af3806`
- **Työkalusäännöt:**
  1. Älä koskaan kutsu `list_notebooks` -työkalua (ID on jo lukittu).
  2. Käytä hauissa (`retrieve_relevant_chunks`, `query_notebook`) AINA yllä olevaa ID:tä.
  3. ÄLÄ koskaan lue tai huomioi tiedostoja kansioista `_archive_docs/` tai `scratch/`.

---

## 2. PELIKONSEPTI & YDINSÄÄNNÖT (v2.6)

1. **D20-Sääntöjärjestelmä:**
   - Kaikki toiminnot ratkaistaan kaavalla: `d20 + bonus >= AC / DC`. Nat 20 = tuplavahinko, Nat 1 = automaattinen huti.
   - Älykäs nopanheiton pysäytys (`DiceUIController`): Jos pelaajalla on *Uudelleenheitto-käärö (Rune of Reroll)*, heitto pysähtyy valintaan `[Jatka]` tai `[Käytä käärö]`. Ilman kääröä heitto etenee automaattisesti 1.0 sekunnin jälkeen.
2. **Sankariluokat (Sooloseikkailu):**
   - 🛡️ **Soturi (Sir Roland):** 30 HP, 14 AC, Liike 4. Kyvyt: *Miekansivallus*, *Kilpimuuri* (+4 AC & vastaisku), *Sotahuuto* (3x3 työntö), *Rautainen tahto* (parannus).
   - 🔮 **Velho (Oppinut Elira):** 20 HP, 12 AC, Liike 3. Kyvyt: *Jäänsäde*, *Tulipallo* (3x3 AoE), *Mana-kilpi* (100% absorptio), *Blink* (5–7 ruudun teleportti).
   - 🗡️ **Varas (Varjo-Corvo):** 25 HP, 13 AC, Liike 5. Kyvyt: *Myrkkytikari*, *Selkäänpuukotus* (Advantage + tuplavahinko), *Savupommi*, **Varjoaskel (Shadow Step)** (3 ruudun teleportti + Advantage). *Tiirikointi* on passiivinen tutkimustaito (`LockpickInteraction.cs`).
3. **12x12 Taktinen Ruudukko & Taistelutempon Eristys:**
   - Taistelualueet luodaan 12x12 koossa (`gridWidth = 12`, `gridHeight = 12`, `tileSize = 1.6f`) `GridManager.cs`- ja `DungeonRoomController.cs`-ohjaimilla.
   - Taisteluissa on **1 eliitti tai max 2 vihollista**. Pomon apujoukot on rajoitettu 1 yksikköön.
   - Voitetusta taistelusta myönnetään aina 2–10 romumetallia (`TurnManager.AwardCombatVictoryScrap()`).
4. **7 Pelialueen Maailmankartta:**
   - `0: Zone_1_VillageAndCellar` (Kylä, paja, taverna, kellari)
   - `1: Zone_2_ForestPath` (Metsäpolku, DC 13 salaportti)
   - `2: Zone_3_CastleCourtyard` (1. Pomo: Kirottu Komentaja)
   - `3: Zone_4_Library` (2. Pomo: Varjomaagi Malakor)
   - `4: Zone_5_CastleHall` (Keskushalli, turva-alue & `SavePoint.cs` Ruunikivialttari)
   - `5: Zone_6_Tower` (Aarretorni: Othelian sormus & +30 Max HP Eliksiiri)
   - `6: Zone_7_ThroneRoom` (3. Pomo: Kivettymiskuningas)
5. **Kylätalous (Seppä Baldur):**
   - Romumetallin myynti: 1 romu = 10 Kultaa.
   - Ostot: Terveysjuoma (15g tai 25g), Teroitettu miekka (+1 Pysyvä DMG, 50-60g), Haarniskapäivitys (+1 Pysyvä AC, 50-100g).

animator.SetTrigger("Attack");
yield return new WaitForSeconds(0.35f);
target.TakeDamage(damageAmount);
SFXManager.Instance.PlaySFX(SFXClipType.SwordHit, transform.position);

---

## 3. UNITY 6 & WEBGL -ARKKITEHTUURISÄÄNNÖT

1. **Dual Input Handling (`Active Input Handling = Both`):**
   - Player Settingsissä pidetään päällä `Both` (tukee editorityökaluja ja New Input Systemiä).
   - Kaikissa skeneissä `EventSystem`-objektissa on `InputSystemUIInputModule` (`module.AssignDefaultActions()`).
   - Pelin syöte haetaan New Input Systemin kautta (`GameInput.cs`). Tilavaihdoissa kytketään kartat puhtaasti:
     `inputActions.Exploration.Disable(); inputActions.Combat.Enable();`
2. **WebGL Air-Gap & Zero-GC Muistisäännöt:**
   - Älä koskaan tee `System.IO`-kovalevykutsuja tai ulkoisia HTTP-kutsuja pelisilmukassa.
   - Tallennus: JSON-sarjallistus selaimen `PlayerPrefs`-muistiin (`SaveSystem.cs`).
   - Äänentoisto: Aina 12x AudioSource -poolauksen kautta (`SFXManager.cs`). Ei jatkuvaa `Instantiate`/`Destroy`-kutsua.
   - Ei LINQ-hakuja (`.Where()`, `.Select()`) eikä jatkuvaa merkkijonokonkatenaatiota `Update()`-metodeissa.
3. **Tagit ja Layerit:**
   - `Layer 6`: `GridTile` | `Layer 7`: `Obstacle` | `Layer 8`: `Interactable` | `Layer 9`: `Unit`
4. **Persistent Managers (`DontDestroyOnLoad`):**
   - Aloitusskeneen sijoitetaan `_CoreManagers`: `GameManager`, `SceneLoader`, `MusicManager`, `SFXManager`, `PlayerProgressionManager`.
5. **UI Canvas (1920x1080 Match 0.5):**
   - `Hero_Status_Card` (Top-Left): Crest, HP-bar, Kulta, Romu, Pika-HP-juoma [Q].
   - `Zone_Indicator_Banner` (Top-Center): Alueen nimi.
   - `Quest_Tracker_Card` (Top-Right): Tehtävät (`QuestHUDUIController.cs`).
   - `CombatActionBar` (Bottom-Center): 4 kykyä + End Turn -nappi tiimalasilla.
   - `DiceModalPanel` (Center): D20-nopanheittoruutu.

---

## 4. C#-KOODAUSKAAVAT

### D20-Heitot:
```csharp
DiceResult result = DiceSystem.RollD20(bonus: player.PrimaryAttributeBonus, targetDC: enemy.ArmorClass);
DiceResult advResult = DiceSystem.RollD20(bonus: player.PrimaryAttributeBonus, targetDC: 14, advantage: AdvantageType.Advantage);