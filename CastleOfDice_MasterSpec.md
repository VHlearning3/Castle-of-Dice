# Castle of Dice — Master Architecture & Game Specification (MasterSpec)

**Versio:** 2.6  
**Kohdeympäristö:** Unity 6 (6000.3.14f1 LTS) WebGL (60 FPS, WebAssembly, URP 17.3.0)  
**Kieli:** C# (.NET Standard / Unity C#)  
**Kehystyyppi:** Turn-Based Tactical RPG / Tabletop D20 Simulator  

---

## 1. Yleiskatsaus ja Pelikonsepti

**Castle of Dice** on selaimessa toimiva 3D-pöytäroolipeli (Tabletop Tactical RPG), joka yhdistää taktisen vuoropohjaisen taistelun, D20-noppamekaniikan ja matalapolyvisuaalisuuden (Low-Poly Art Style). 

Peli simuloi perinteistä D&D-pöytäroolipelikokemusta ilman monimutkaisia sääntökirjoja. Pelaaja valitsee seikkailun alussa yhden kolmesta sankariluokasta (Soturi, Velho tai Varas) ja matkaa Oakhavenin kylästä Kirottuun Linnaan voittamaan linnan kolme päävastustajaa.

---

## 2. Tekniset Raamit & Unity 6 WebGL -Arkkitehtuuri

### 2.1 Engine & Kohdeversio
* **Unity-versio:** Unity 6 (6000.3.14f1 LTS)
* **Build Target:** WebGL (Optimoidut tekstuurit, matala muistijalanjälki, koodin strippaus)
* **Resoluutio & UI Scaler:** 1920x1080 (Scale With Screen Size, Match = 0.5)
* **Syötelaite:** Näppäimistö ja hiiri.

### 2.2 Syötejärjestelmä (Dual Input Handling: New Input System + Legacy Input)
Sallitaan sekä uusi `com.unity.inputsystem`-paketti (1.19.0) että perinteinen `UnityEngine.Input`-luokka samanaikaisesti (Unity Player Settings -> **Active Input Handling = Both**).

* **Miksi molemmat syötejärjestelmät pidetään päällä?**
  1. **Täysi yhteensopivuus:** Takaa 100 % yhteensopivuuden kaikkien editorityökalujen (kuten `EnvironmentHazardPainterWindow.cs`), kolmannen osapuolen pakettien ja mukautettujen debug-skriptien kanssa.
  2. **Pelin pääohjaus:** Pelin varsinainen peliohjaus (`GameInput.cs`, `PlayerExplorationMovement.cs`, `CombatUIController.cs`) ja UI-kytkennät ohjataan uuden Input Systemin kautta (`InputSystemUIInputModule` kytketyillä `module.AssignDefaultActions()`-toiminnoilla).
* **EventSystem-määritelmä:** Skeneissä käytetään `InputSystemUIInputModule`-komponenttia, joka osaa käsitellä hiiren osoittimen ja näppäimistön syötteet luotettavasti molemmissa tiloissa.

### 2.3 Skenerakenne & Pelaajadatan Säilyvyys (Data-Driven Persistence)
Selaimen muistivuotojen estämiseksi peli on jaettu aluekohtaisiin skeneihin:
1. **Persistent Managerit (`DontDestroyOnLoad`):** `GameManager`, `MusicManager`, `SFXManager`, `SceneLoader`.
2. **Data-Objektit:** Pelaajan tilastot (HP, Kulta, Varusteet, Kykyjen tasot) säilyvät `PlayerDataSO` ScriptableObjectissa ja `SaveSystem.cs`-luokassa.
3. **Pelaaja-spawnaus:** Jokaisessa alueskenessä on `StartSpawnPoint.cs`-objekti, joka instanssoi pelaajan `PlayerUnit`-prefabin ja lukee sille arvot `PlayerDataSO`-oliosta.

### 2.4 Dynaaminen 12x12 Taisteluruudukko & Encounter Isolation
* **Ruudukko:** Luodaan huoneen koon mukaan peli-ajossa (`12x12` -matriisi, 144 ruutua, `tileSize = 1.6f`).
* **Seinät ja Esteet:** Generoinnissa ajetaan fysiikkakysely `Physics.CheckSphere(tilePos, 0.4f, obstacleLayerMask)`. Jos este löytyy, asetetaan `GridTile.isWalkable = false`.
* **Encounter Isolation:** `DungeonRoomController.cs` toimii taistelualueen laukaisimena. Pelaajan astuessa huoneeseen:
  1. Ulostuloportit lukittuvat (`exitBarriers` active).
  2. 12x12 `GridManager` generoidaan.
  3. Huoneeseen sijoitetut viholliset aktivoituvat.
  4. `GameManager.SetMode(GamePlayMode.Combat)` ja `TurnManager.StartCombat()` käynnistyvät.
  5. Taisteluvoiton jälkeen portit aukeavat, palkitaan 2–10 romumetallia (`TurnManager.AwardCombatVictoryScrap()`) ja palataan tutkimustilaan.

### 2.5 Animaatiot ja Vahinkolaskenta
* **Animator-triggerit:** Kaikilla 3D-malleilla on vakioidut parametrit: `"Idle"`, `"Walk"`, `"Attack"`, `"CastMagic"`, `"Hurt"`, `"Die"`.
* **Vahingonlaskennan ajoitus:** Käytetään koodipohjaista viivästystä (`Coroutine` / `yield return new WaitForSeconds(0.35f)`) animaation iskuhetken kohdalla Animation Event -virheiden vältämiseksi.

### 2.6 Vihollistekoäly & Soolosankarin Tempo
* **Määrärajoitus:** Tavalliset taistelut sisältävät 1 eliittivihollisen tai max 2 vihollista. Pomot kutsuvat max 1 apurin kerrallaan (`skeletonCount = 1`, `decoyCount = 1`).
* **Tekoäly:** Vihollinen (`EnemyUnit.cs`) etsii hyökkäyskantamallaan olevat kohteet ja valitsee kohteensa satunnaisesti tai pääkohteen mukaan.

---

## 3. Pelimaailma & Maailmankartta (7 Zone-Aluetta)

Pelialue noudattaa pohjakarttaa (`clear_map.png`):

```text
               [ 3. BOSS: Kivettymiskuningas ]
                             ▲
                             │ (Vaatii 1. tai 2. Pomon voiton)
                             │
     ┌───────────────────[ HALL ]───────────────────┐
     │          (Turva-alue & SavePoint)           │
     │                                              │
     ▼                                              ▼
[ 2. BOSS: Malakor ]                       [ TOWER: Aarrekammio ]
  (Kirjasto)                                 (+30 Max HP Eliksiiri)
     ▲
     │ (Salareitti)
[ PUZZLE + GATE ]
     ▲
     │
[ FOREST ] ───────────────────────────────► [ 1. BOSS: Komentaja ]
     ▲                                       (Alapiha)
     │
[ VILLAGE & CELLAR ] (Oakhaven, Kauppa, Rottatehtävä)
     ▲
[ START ]
```

### 3.1 Alueiden Toiminnallisuudet
1. **`Zone_1_VillageAndCellar`:** Aloituspiste ja Oakhavenin kylä (Seppä Baldur, Krouvinisäntä Barnaby, Vanhin Othelia, Parantaja Mirabel) sekä Viinikellarin tuholaistaistelu.
2. **`Zone_2_ForestPath`:** Metsäpolku ja tiirikoitava salaportti (`PUZZLE + GATE`, DC 13).
3. **`Zone_3_CastleCourtyard`:** Linnan alapiha & 1. Pomo: **Kirottu Komentaja**.
4. **`Zone_4_Library`:** Salatieteen siipi & 2. Pomo: **Varjomaagi Malakor**.
5. **`Zone_5_CastleHall`:** Keskushalli / `HALL` (Turvaalue, `SavePoint.cs` Ruunikivialttari – palauttaa HP:n 100 % ja tallentaa pelin JSON-muodossa `PlayerPrefs`-muistiin `SaveSystem.cs`-luokan kautta).
6. **`Zone_6_Tower`:** Piilotettu Aarretorni (Othelian sormus & *Jättiläisen eliksiiri*: +30 permanent Max HP).
7. **`Zone_7_ThroneRoom`:** Kruununsali & 3. Pomo: **Kivettymiskuningas**.

---

## 4. Pelimekaniikat & Sääntöjärjestelmä

### 4.1 D20-Noppajärjestelmä
Kaikki hyökkäykset, taitoheitot ja dialogitestit ratkaistaan yhtälöllä:
$$\text{d20-heitto} + \text{Taitobonus} \ge \text{AC / DC}$$
* **Luonnollinen 20 (Nat 20):** Kriittinen osuma (Tuplavahinko / automaattinen läpäisy).
* **Luonnollinen 1 (Nat 1):** Kriittinen huti (Automaattinen epäonnistuminen).
* **Etu / Haitta (Advantage / Disadvantage):** Heitetään $2\text{d20}$ ja valitaan korkeampi/alempi tulos.

### 4.2 Nopan Heittoruutu & Älykäs Pysäytys (`DiceUIController.cs`)
* **Animaatio:** D20-noppa pyörii näytöllä 0.8 sekuntia heittoäänen kera.
* **Automaattinen jatko:** Jos pelaajalla ei ole Reroll-kääröä, tulos näytetään ~1.0 s ja ruutu sulkeutuu automaattisesti.
* **Pysäytys & Miettimistauko:** Jos pelaajalla on Reroll-käärö (`RuneOfRerollController.cs`), näytetään painikkeet `[ Jatka ]` ja `[ 📜 Käytä Uudelleenheitto-kääröä ]`.

### 4.3 Hahmoluokat & Soolosankarit
1. **Soturi (Sir Roland Rautakoura):** 30 HP, 14 AC, Move 4, STR (+3).
   * *Miekansivallus:* 1d8+3 pääkohteelle + puolet viereiselle.
   * *Kilpimuuri:* +4 AC vuoroksi, vastaisku 1d6 jos huti.
   * *Sotahuuto:* 3x3 shockwave, työntää 1–2 ruutua + 1d4+3 dmg.
   * *Rautainen tahto:* Palauttaa 30 % Max HP, poistaa debuffit.
2. **Velho (Oppinut Elira):** 20 HP, 12 AC, Move 3, INT (+3).
   * *Jäänsäde:* Kantama 4, 1d6+3 dmg, puolittaa liikkeen 1 vuoroksi.
   * *Tulipallo:* Kantama 4, 3x3 AoE, 2d6 fire dmg.
   * *Mana-kilpi:* Absorboi 100 % seuraavasta osumasta.
   * *Blink (Teleportti):* Siirtyy 5–7 ruutua ilman vastahyökkäyksiä.
3. **Varas (Varjo-Corvo):** 25 HP, 13 AC, Move 5, AGI (+3).
   * *Myrkkytikari:* 1d4+3 initial + 1d6 myrkky/vuoro 2 vuoron ajan.
   * *Selkäänpuukotus:* 2d20 Advantage, 2d6+3 dmg (tupla jos sokea tai Varjoaskel).
   * *Savupommi:* Kantama 3, 3x3 AoE, sokeuttaa 1 vuoroksi (Disadvantage vihollisille).
   * *Varjoaskel (Shadow Step):* Teleportti 3 ruutua + auto Advantage seuraavaan hyökkäykseen.
   *(Tiirikointi on passiivinen maastotaito LockpickInteraction.cs -skriptissä).*

---

## 5. Pre-Combat Dialogidebuffit & Boss Mechanics

1. **Kirottu Komentaja (Alapiha):** 50 HP, 16 AC. Kutsuu 1 luurangon 50 % HP:ssa.  
   *Dialogi (`Soldier's Honor | DC 13`):* `CommanderArmorWeakened` (-2 AC 2 vuoroksi -> 14 AC).
2. **Varjomaagi Malakor (Kirjasto):** 40 HP, 13 AC. Teleporttaa vahingosta, luo 1 peilikuvakloonin.  
   *Dialogi (`Arcane Heresy | DC 14`):* `ArcaneHeresy` (merkitsee aito-Malakorin `[True]`-badgella).
3. **Kivettymiskuningas (Kruununsali):** 60 HP, 15 AC (Vaihe 1) -> 18 AC (Vaihe 2 Stone Form).  
   *Dialogi (`Intimidation | DC 16`):* `Intimidated` (-3 hyökkäysvahinkoa 3 vuoroksi).

---

## 6. Talous, Tehtävät & HUD Layout

* **Romumetalli & Paja:** Taisteluista 2–10 romua (`TurnManager.AwardCombatVictoryScrap()`). Seppä Baldur: 1 romu = 10g, Potion = 25g (+15 HP), Teroitettu terä = 60g (+1 DMG), Riimukilpi = 100g (+1 AC).
* **HUD Layout:** Canvas 1920x1080. `Hero_Status_Card` (vasen ylä), `Quest_Tracker_Card` (oikea ylä), `DiceUIController` (keski-modal).
