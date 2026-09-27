# Castle of Dice — Master Architecture & Game Specification (MasterSpec)

**Versio:** 1.0  
**Kohdeympäristö:** Unity 6 (6000.3.14f1 LTS) WebGL  
**Kieli:** C# (.NET Standard / Unity C#)  
**Kehystyyppi:** Turn-Based Tactical RPG / Tabletop D20 Simulator  

---

## 1. Yleiskatsaus ja Pelikonsepti

**Castle of Dice** on helppokäyttöinen, selaimessa toimiva 3D-pöytäroolipeli (Tabletop Tactical RPG), joka yhdistää taktisen vuoropohjaisen taistelun, D20-noppamekaniikan ja matalapolyvisuaalisuuden (Low-Poly Art Style). 

Peli simuloi perinteistä D&D-pöytäroolipelikokemusta ilman monimutkaisia sääntökirjoja tai erillisiä asennuksia. Pelaaja valitsee seikkailun alussa yhden kolmesta sankariluokasta (Soturi, Velho tai Varas) ja matkaa Oakhavenin kylästä Kirottuun Linnaan voittamaan linnan kolme päävastustajaa.

---

## 2. Tekniset Raamit & Unity 6 WebGL -Arkkitehtuuri

### 2.1 Engine & Kohdeversio
* **Unity-versio:** Unity 6 (6000.3.14f1 LTS)
* **Build Target:** WebGL (Optimoidut tekstuurit, matala muistijalanjälki, koodin strippaus)
* **Resoluutio & UI Scaler:** 1920x1080 (Scale With Screen Size, Match = 0.5)
* **Syötelaite:** Pelkästään näppäimistö ja hiiri.

### 2.2 Syötejärjestelmä (Unity New Input System)
Käytetään `com.unity.inputsystem`-pakettia syötestuck-ongelmien estämiseksi WebGL-selaimessa.
* **`Exploration` Action Map:**
  * Liikkuminen: `WASD` / Nuolinäppäimet
  * Interaktio: `E` / `Space`
  * Valikko: `ESC` (Pause)
* **`Combat` Action Map:**
  * Ruudukon/kohteiden valinta: Hiiren vasen näppäin (`LeftClick`)
  * Pikanäppäimet kyvyille: `1`, `2`, `3`, `4`
* **Lukitus:** Vuorojen vaihtuessa (`EnemyTurn`) tai dialogissa syötekartta disabloidaan kooditasolla: `inputActions.Exploration.Disable()`.

### 2.3 Skenerakenne & Pelaajadatan Säilyvyys (Data-Driven Persistence)
Selaimen muistivuotojen estämiseksi peli on jaettu aluekohtaisiin skeneihin:
1. **Persistent Managerit (`DontDestroyOnLoad`):** `GameManager`, `MusicManager`, `SFXManager`, `SceneLoader`.
2. **Data-Objektit:** Pelaajan tilastot (HP, Kulta, Varusteet, Kykyjen tasot) säilyvät `PlayerDataSO` ScriptableObjectissa ja `SaveSystem.cs`-luokassa.
3. **Pelaaja-spawnaus:** Jokaisessa alueskenessä (`Zone_Village`, `Zone_ForestPath`, `Zone_CastleLobby` jne.) on `SpawnPoint`-objekti, joka instanssoi pelaajan `PlayerUnit`-prefabin ja lukee sille arvot `PlayerDataSO`-oliosta.

### 2.4 Dynaaminen Taisteluruudukko & Reitinhaku
* **Ruudukko:** Luodaan huoneen koon mukaan peli-ajossa (`GridTile[,]`-matriisi).
* **Seinät ja Esteet:** Generoinnissa ajetaan fysiikkakysely `Physics.CheckSphere(tilePos, 0.4f, obstacleLayerMask)`. Jos este löytyy, asetetaan `GridTile.isWalkable = false`.
* **Reitinhaku:** Taistelun aikana tekoäly ja liikkuminen lukevat muistimatriisia (`GridTile[,]`) BFS-algortimilla ilman raskaita fysiikkakyselyitä.

### 2.5 Animaatiot ja Vahinkolaskenta
* **Animator-triggerit:** Kaikilla 3D-malleilla on vakioidut parametrit: `"Idle"`, `"Walk"`, `"Attack"`, `"CastMagic"`, `"Hurt"`, `"Die"`.
* **Vahingonlaskennan ajoitus:** Käytetään koodipohjaista viivästystä (`Coroutine` / `yield return new WaitForSeconds(0.35f)`) animaation iskuhetken kohdalla. Vältetään hauraita Animation Event -kutsuja.

### 2.6 Vihollistekoäly (Enemy AI)
* Vihollinen (`EnemyUnit.cs`) etsii hyökkäyskantamallaan olevat sankariyksiköt ja valitsee kohteensa **satunnaisesti** arvalla (`Random.Range(0, validTargets.Count)`).

---

## 3. Pelimaailma, Pohjakartta & Eteneminen

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
1. **START & VILLAGE + CELLAR:** Aloituspiste ja Oakhavenin kylä.
   * NPC-hahmot: Seppä Baldur, Krouvinisäntä Barnaby, Vanhin Othelia, Parantaja Mirabel.
   * Viinikellari: Tutoriaalitaistelu jättirottia vastaan (*Viinikellarin tuholaiset*).
2. **FOREST & PUZZLE + GATE:** Metsäpolku ja salareitti.
   * Varas voi tiirikoida salaportin (`PUZZLE + GATE`), joka vie suoraan Kirjastoon (2. Pomo) ohittaen 1. Pomon.
3. **1. BOSS (Alapiha & Vartiotorni):** **Kirottu Komentaja** (50 HP, 16 AC, kutsuu luurankoja).
4. **2. BOSS (Kirjasto & Salatieteen siipi):** **Varjomaagi Malakor** (40 HP, 13 AC, teleporttaa ja luo peilikuvia).
5. **HALL (Keskushalli / Foyer):** Turvallinen lepopaikka.
   * Sisältää **Ruunikivialttarin / Tallennuspisteen (`SavePoint.cs`)**, joka palauttaa 100 % HP:sta ja tallentaa pelin (`SaveSystem.cs`).
6. **TOWER (Torni):** Piilotettu aarrekammio Keskushallin oikealla puolella.
   * Sisältää **Legendaarisen jättiläisen eliksiirin**, joka antaa pelaajalle **pysyvän +30 Max HP** -lisäyksen.
7. **3. BOSS (Kruununsali):** **Kivettymiskuningas** (60 HP, 15 AC -> 18 AC Stone Form).

### 3.2 Etenemissäännöt & Edestakainen Liikkuminen
* **Etenemisehto 3. Pomolle:** Pelaajan **ei tarvitse voittaa molempia** edeltäviä bosseja. Pääsy Keskushalliin ja 3. Pomolle aukeaa voittamalla joko 1. Pomon tai 2. Pomon.
* **Avoimet ovet:** Ovet eivät sulkeudu lukkoon pelaajan takana. Pelaaja voi aina palata Kivenkolon kylään parantumaan, ostamaan varusteita tai palauttamaan tehtäviä.

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
* **Automaattinen jatko (Pelaajalla EI ole Reroll-kääröä):** Tulos näytetään ~1.0 sekuntia, minkä jälkeen ruutu sulkeutuu ja peli jatkuu välittömästi ilman turhia klikkauksia.
* **Pysäytys & Miettimistauko (Pelaajalla ON Reroll-käärö):** Nopan tulos pysähtyy näytölle. Pelaajalle tarjotaan kaksi painiketta:
  1. `[ Jatka ]` – Hyväksyy heittotuloksen.
  2. `[ 📜 Käytä Uudelleenheitto-kääröä (x kpl) ]` – Kuluttaa 1 käärön ja heittää nopan uudelleen samaa DC/AC-arvoa vastaan.

### 4.3 Uudelleenheitto-käärö (Reroll Scroll / Riimukivi)
* Universaali resurssi, jota voi käyttää kaikkiin nopanheittoihin (taistelu, dialogi, tiirikointi).
* Pinoaminen on rajatonta, mutta niitä löytää koko pelin aikana vain muutaman kappaleen (harvinainen teho-esine).

### 4.4 Hahmoluokat & Yksittäinen Sankari
Pelin alussa valitaan yksi sankari koko seikkailun ajaksi:
* **Soturi (Sir Roland Rautakoura):** 30 HP, 14 AC. Kyvyt: *Miekansivallus*, *Kilpitorjunta* (+3 AC), *Sotahuuto* (työntö), *Rautainen tahto* (palauttaa 30 % HP).
* **Velho (Oppinut Elira / Elisa Tähtisilmä):** 20 HP, 12 AC. Kyvyt: *Tulipallo* (3x3 AoE), *Jääriite* (puolittaa liikkeen), *Mana-kilpi*, *Teleportti* (5 ruutua).
* **Varas (Varjo-Corvo):** 25 HP, 13 AC. Kyvyt: *Selkäänpuukotus* (Advantage + tuplabonus), *Savupommi* (sokeutus), *Myrkkytikari* (1d6 myrkky/vuoro), *Tiirikointi & Ansanpurku*.

### 4.5 Tasonnousu & Milestone-järjestelmä
Tasonnousu tapahtuu linnan siipipomojen jälkeen (**Level 1 -> Level 2 -> Level 3**). Jokaisella tasonnousulla pelaaja valitsee yhden edun:
1. **+5 Max HP** & täysparannus.
2. **+1 Attribute Bonus** (Soturi: Voima, Velho: Älykkyys, Varas: Ketteryys -> nostaa osumatarkkuutta).
3. **Kyvyn päivitys (Ability Rank 2)**.

---

## 5. NPC:t, Tehtävät ja Talous (Economy Loop)

### 5.1 NPC-Interaktiot
* NPC:t seisovat paikoillaan Oakhavenin kylässä. Pään yläpuolella on 3D-merkki.
* Interaktio käynnistetään **klikkaamalla NPC-hahmoa hiirellä**.
* Kun tehtävä on suoritettu, dialogi muuttuu muotoon *"Thanks for help!"* ja NPC antaa palkkion.

### 5.2 Sivutehtävät & Tavoitteet
1. **Krouvinisäntä Barnaby – *Viinikellarin tuholaiset*:**
   * Tavoite: Tapa jättirotat (`3/3`). Päivittyy kellaritaistelussa rottien kuollessa.
2. **Vanhin Othelia – *Kadonnut perintökalleus*:**
   * Tavoite: Etsi Othelian sinettisormus (`1/1`). Piilotettuna Aarretornissa (`TOWER`).
3. **Parantaja Mirabel – *Suokukat Mirabelille*:**
   * Tavoite: Kerää suokukkia (`5/5`). Poimitaan hiiriklikkauksella kylän ympäriltä ja metsästä.

### 5.3 Tehtäväpalkinto & Esinesuuojaus
* Tehtäväesineitä (sormus, suokukat) ei voi pudottaa, myydä tai tuhota.
* Jokaisen tehtävän palauttamisesta saatava kiinteä palkinto: **50 Kultaa + 2x Terveysjuomaa (HP Potion)**.

### 5.4 Kylätalous & Romumetalli
* **Romumetalli (Scrap Metal):** Saadaan **vain voitetun taistelun jälkeen** satunnaisena pudotuksena: **2–10 kpl / taistelu**.
* **Seppä Baldur (Kylän paja):**
  * Myy romumetallia: 1 romu = 10 Kultaa.
  * Osta Pieni Terveysjuoma: 25 Kultaa (+15 HP).
  * Osta Teroitettu Miekka: 60 Kultaa (+1 Pysyvä vahinkobonus).
  * Osta Riimukilpi / Haarniska: 100 Kultaa (+1 Pysyvä AC-puolustus).

---

## 6. Käyttöliittymäjärjestelyt (HUD Layout)

Käyttöliittymä (`PlayerHUD`) on jaettu selkeisiin kortteihin ruudun kulmissa:

```text
 ┌────────────────────────────────────────────────────────────────────────┐
 │ PlayerHUD (Canvas: 1920x1080)                                          │
 │                                                                        │
 │ [Hero_Status_Card] (Vasen yläreuna)     [Quest_Tracker_Card] (Oikea ylä)│
 │  ❤️ HP: 30/30                            📜 TEHTÄVÄT                [−]│
 │  🟡 Kulta: 120g                         • Viinikellarin tuholaiset    │
 │  🔩 Romumetalli: 8 kpl                    Tapa jättirotat: 2/3         │
 │  🧪 HP-Juomat: 3 kpl                    • Othelian sormus: 1/1       │
 │                                         • Suokukat: 5/5 (Valmis)       │
 └────────────────────────────────────────────────────────────────────────┘
```

1. **`Hero_Status_Card` (Ruudun vasen yläreuna):**
   * Hahmon hengenpelastustiedot ikonien kera: HP-palkki, Kulta, Romumetalli ja HP-juomat.
2. **`Quest_Tracker_Card` (Ruudun oikea yläreuna):**
   * Näyttää kaikkien 3 tehtävän edistymisen muodossa `X/X`.
   * Pienennettävissä kulmassa olevalla `[−]`-painikkeella.
   * Suoritetut tehtävät muuttuvat kuittauksen jälkeen läpinäkyviksi (`alpha = 0.5`).
3. **`DiceUIController` (Ruudun keskipiste / Modal):**
   * D20-nopanheittoruutu, joka avautuu taistelun, tiirikoinnin ja dialogitestien aikana.

---

## 7. Toteutetut C#-Skriptit & Komponentit

Projektin koodipohja koostuu seuraavista Studio-paneelissa sijaitsevista C#-skripteistä:

| Skripti / Komponentti | Vastuualue & Kuvaus |
| :--- | :--- |
| **`MusicManager.cs`** | Kaksikanavainen koodipohjainen musapuolen crossfader (Ambient & Combat BGM). |
| **`ZoneMusicSO.cs`** | ScriptableObject aluekohtaisten musiikkiraitojen määrittelyyn. |
| **`SFXManager.cs`** | Keskitetty 12x AudioSource -pooli 2D/3D-ääniefekteille ja pitch-säätöineen. |
| **`SceneLoader.cs`** | Asynkroninen skenelataaja WebGL-nykimisen estämiseksi + d20-vinkkimatriisi. |
| **`QuestHUDUIController.cs`** | Oikean yläreunan minimoitava tehtäväkortti ja tavoiteseuranta. |
| **`QuestEntryUI.cs`** | Yksittäisen tehtäväkortin valikkoelementti ja `X/X`-laskuri. |
| **`RuneOfRerollController.cs`**| D20-heittomodaalin Uudelleenheitto-käärön kytkentä ja tarkistus. |
| **`FloatingCombatText.cs`** | Maailmantilan kameransuuntainen vahinkonumero- ja status-tekstimoduuli. |
| **`StatusEffectVisualOverlay.cs`**| Hiukkasefektit (Myrkkykuplat, Jääaura, Mana-kilpi) 3D-hahmojen ylle. |
| **`GridEnvironmentHazard.cs`** | Ansa- ja vaararuudut (esim. Myrkkyammallas, Tulisilmä) ruudukolla. |
| **`EnvironmentHazardSO.cs`** | ScriptableObject ansojen vahingoille, DC-tarkistuksille ja tehosteille. |
| **`EnvironmentHazardPainterWindow.cs`**| Unity Editor -työkalu ansojen maalaamiseen suoraan Scene-näkymässä. |
| **`LootTableSO.cs`** | D20-pohjainen arvontataulukko tavaroille ja kultamäärille. |
| **`ChestLootDrop.cs`** | Aarrearkkujen ja pomojen pudotusgeneraattori. |
| **`PlayerProgressionManager.cs`**| Milestone-tasonnousut (Level 1 -> 2 -> 3) ja attribuuttibonukset. |
| **`LevelUpUIController.cs`** | Tasonnousun valikkoikkuna ja 3 valintapolkua. |
| **`NaturePathSecretDoor.cs`** | Metsäpolun salareitti Kirjastoon varkaan tiirikoinnilla. |
| **`LockpickMinigameUIController.cs`**| Tiirikoinnin D20-tarkistusikkuna ja animaatio. |
| **`EnemyDataSO.cs` & `ConfigurableEnemyUnit.cs`**| Vihollisten tilastot, tekoäly ja dynaamiset parametrit. |
| **`CharacterDataSO.cs` & `CharacterSkillSO.cs`**| Hahmoluokkien ja 4 aktiivisen kyvyn tietokanta. |

---

## 8. Kokoamis- ja Yhdistämisohje (Unity 6 WebGL Project Setup)

1. **Projektin luonti:** Luodaan uusi Unity 6 (6000.3.14f1) 3D-projekti.
2. **Paketit:** Asennetaan `Input System` (`com.unity.inputsystem`) ja `TextMeshPro`.
3. **Koodien siirto:** Kopioidaan kaikki 31 skriptiä Studio-paneelista kansion `Assets/Scripts/` alle.
4. **Canvas-asettelu:**
   * Luodaan Canvas, jonka `Canvas Scaler` asetetaan arvoon `1920x1080` (Scale With Screen Size).
   * Kiinnitetään `Hero_Status_Card` vasempaan yläreunaan ja `Quest_Tracker_Card` oikeaan yläreunaan.
5. **Persistent Managerit:** Sijoitetaan skeneen `_GameManager`, `_MusicManager`, `_SFXManager` ja `_SceneLoader`.
6. **Build Settings:** Lisätään alueskenet (`Zone_Village`, `Zone_ForestPath`, `Zone_CastleLobby` jne.) skenelistaan ja valitaan kohdealustaksi **WebGL**.

---
*Dokumentti luotu automaattisesti yhdistämällä Castle of Dice -projektin lähdemateriaalit, kartat ja C#-arkkitehtuuri.*
TargetFile: /workspace/scratch/CastleOfDice_MasterSpec.md
