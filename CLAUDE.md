# 🎲 CASTLE OF DICE — MASTER AGENT & ARCHITECTURE DIRECTIVES (v2.7)

Tämä tiedosto on Antigravity- ja Claude Code -agenttien ylin ohjesääntö ja totuuden lähde (Source of Truth).
Kohdeympäristö: **Unity 6 (6000.3.14f1 LTS) WebGL (60 FPS)** | C# (.NET Standard) | URP Low-Poly.
Päivitetty 6.10.2026 vastaamaan paikallista masteria (b065192).

---

## 1. KNOWLEDGE BASE & GROUNDING (Strict NotebookLM Policy)

- Kaikkien alagenttien ja orkestraattorin AINOA sallittu ulkoinen tietolähde on:
  - **Notebook ID:** `ab6acec3-5413-4df2-8352-5fd173af3806`
  - **Resource Name:** `notebooks/ab6acec3-5413-4df2-8352-5fd173af3806`
- **Työkalusäännöt:**
  1. Älä koskaan kutsu `list_notebooks` -työkalua (ID on jo lukittu).
  2. Käytä hauissa (`retrieve_relevant_chunks`, `query_notebook`) AINA yllä olevaa ID:tä.
  3. ÄLÄ koskaan lue tai huomioi tiedostoja kansioista `_archive_docs/` tai `scratch/`.
- Jos NotebookLM ja koodi ovat ristiriidassa, **koodi ja tämä tiedosto voittavat**.

---

## 2. TYÖSKENTELYSÄÄNNÖT (lue ennen kuin kosket skeneihin)

1. **Kylä (Zone 1) on Vilin käsin muokkaama.** `Zone_1_VillageAndCellar`-skenen `Village_Reference`-juurta ei saa koskaan rakentaa uudelleen eikä tallentaa työkalulla. Kylän muutokset tehdään editorissa paikan päällä. Rebuild Village / Build Complete Village Layout / Build Cellar and Door kysyvät ensin, batch-rakennus vaatii `-forceVillageRebuild`. Uudet builderit pidetään saman vartijan takana. Kylän runtime-lisäykset (lore-kirjeet `RuntimeLoreSpawner`, `ZoneExitSign`, `PickupBeacon`) rakennetaan koodissa, ei skeneen.
2. **Editorin käsimuokkaukset pysyvät.** Älä ylikirjoita Vilin skeneen tekemiä muutoksia builderilla. Jos muutos tarvitaan, tee se pienenä in-place-muokkauksena.
3. **`CastleOfDice/…`-valikon builderit korvaavat oman juuriobjektinsa**, jos ne ajetaan uudelleen (muu skene jää ennalleen):
   - `Dress Zones 2-7 (Floor Plans)` → `Zone_Dressing` (`ZoneDressingBuilder`). Ohittaa skenet, joissa on `Forest_Expansion`-juuri, joten Zone 2:n koristelu on nyt vain käsin muokattavaa.
   - `Add Review Content (Zones 2-7)` → `Review_Content` (`ReviewContentBuilder`).
   - `Class Quests/Build All` → `Class_Quests` alueissa 2.5, 4 ja 5 (`ClassQuestBuilder`).
   - `Castle Doors (Double + Single)` → ovet alueissa 3–7 (`CastleDoorSetup`); `Build Hidden Trail (Zone 2.5)` → `SecretForestPathBuilder`.
   - **Älä aja `Build Zone Scenes`** (`ZoneSceneBuilder`): se rakentaa alueet 2–7 tyhjästä ja pyyhkisi myöhemmät lisäykset.
4. **Worktree + batch-tila.** Kun Vilin editori on auki pääkansiossa, työskentele omassa worktreessä `D:\Unity\` alla ja aja Unity batch-tilassa (`-batchmode -executeMethod …`). Testiajot jättävät `Assets/Scenes/Generated/` ja rivinvaihtomuutoksen `ProjectSettings/EditorBuildSettings.asset`-tiedostoon: hylkää ne ennen committia.
5. **Ikonit:** `CoinIcon` ja `HealthPotionIcon` ovat Vilin piirtämiä, niitä ei piirretä uudelleen. Muut 256 px ikonit tehdään `Tools/Icons/gen.js` -skriptillä; editorityökalut eivät saa generoida niitä uudelleen.
6. **Kieli:** pelin tekstit englanniksi, keskustelu Vilin kanssa suomeksi.

---

## 3. PELIKONSEPTI & YDINSÄÄNNÖT

1. **D20-Sääntöjärjestelmä:**
   - Kaikki toiminnot ratkaistaan kaavalla: `d20 + bonus >= AC / DC`. Nat 20 = tuplavahinko, Nat 1 = automaattinen huti.
   - Vihollisen Nat 20 tuplaa vain vahinkonopat, ei kiinteää bonusta (`EnemyUnit.RollCriticalDamage`, 6.10.2026); sankarin Nat 20 tuplaa koko vahingon.
   - Nopanheiton pysäytys (`DiceUIController` + `RuneOfRerollController`): jos pelaajalla on **Rune of Fate** (`item_reroll_rune`), epäonnistunut heitto pysähtyy valintaan `[Jatka]` / `Use Rune of Fate (n)`. Ilman riimua heitto etenee automaattisesti 1.0 sekunnin jälkeen.
2. **Sankarit (sooloseikkailu).** Näyttönimet ovat kiinteät; sisäiset ID:t ja assetit (`Character_Mage_Elira`, `Character_Rogue_Corvo`, `ShadowMageMalakor`, tallennusliput) pidetään tarkoituksella ennallaan. Vanhat nimet (Elira, Corvo, Malakor, Gareth, Aldred, Petrified King) eivät saa näkyä pelaajalle.
   - 🛡️ **Sir Roland the Brave** (Soturi): 30 HP, 14 AC, Liike 4. *Sword Slash* (1d8+STR, puolet vahingosta viereiseen), *Shield Block* (+4 AC & vastaisku 1d6), *War Cry* (viereiset viholliset työntö 1–2 ruutua + 1d4+STR), *Iron Will* (30 % max HP:sta + puhdistus). Taso 4: *Retaliation*.
   - 🔮 **Scholar Elisa Star-Eye** (Velho, nainen): 20 HP, 12 AC, Liike 3. *Frostbite* (kantama 6, 1d6+INT, puolittaa liikkeen), *Fireball* (kantama 4, 2d6+INT 3x3), *Mana Shield* (absorboi seuraavan osuman kokonaan), *Blink* (teleportti enintään 7 ruutua). Taso 4: *Arcane Chains*. Elisa näkee aina Shadow Witchin harhojen läpi.
   - 🗡️ **Sylvaine the Veiled** (Varas, naishaltia): 25 HP, 13 AC, Liike 5. *Poison Dagger*, *Backstab* (2d6+AGI, tuplana sokeaa vastaan tai Shadow Stepin jälkeen), *Smoke Bomb* (3x3 sokaisu), *Shadow Step* (3 ruudun teleportti + Advantage). Taso 4: *Poison Cloud*. *Tiirikointi* on vain varkaan passiivinen tutkimustaito (`LockpickInteraction.cs`, standardi DC 13); Forest Pathin salaportti on vain varkaalle.
3. **Tasot (`PlayerProgressionManager`, `PlayerUnit.MaxLevel = 5`):** taso 1 + neljä virstanpylvästä, kukin +1 taso kerran missä tahansa järjestyksessä:
   1. ensimmäinen voitettu taistelu;
   2. 3 voitettua taistelua TAI Cursed Commander / Shadow Witch hoidettu;
   3. sankarin oma tehtävä (`ClassQuests`);
   4. Tornin haaste (`TowerChallenge`).
   - The Gargoyle King ei anna tasoa. Taistelut tallentuvat `StoryFlags`-lippuina `BattleWon:<scene>/<room>`.
   - Tasonnousun valinnat: +5 Max HP (Hero's Resilience), +1 pääominaisuus, tai kyky Rank 2:een. **Taso 4 avaa viidennen kyvyn** (`PlayerUnit.FifthAbilityLevel`, `CharacterClassSO.level4Ability`), jolle taistelupalkki kloonaa viidennen kortin.
4. **Sankarien omat tehtävät (`ClassQuests`, antajat Baldur / Othelia / Barnaby):** soturi rikkoo laatikot Castle Hallin koilliskulmassa (`BreakableCrates`), velho järjestää Libraryn kirjat ja avaa salahuoneen (`TomeShelfPuzzle` + `TomeShelfMinigameUI`), varas löytää salakuljettajien arkun Hidden Traililta. Palkkio kerran: +1 aseen vahinko ja yksi taso.
5. **Taktinen ruudukko & taistelutempo:**
   - Perustaistelut 12x12 (`DungeonRoomController.gridWidth/gridHeight = 12`, `GridManager.tileSize = 1.6f`). Pomoareenat ovat isompia: Commander 32x32, Shadow Witch 19x19 (alue x -15.5..15.5, z -19..12), Gargoyle King 20x20.
   - Taisteluissa on **1 eliitti tai max 2 vihollista**. Pomon apujoukot rajoitettu 1 yksikköön (Commanderin luuranko 50 % HP:ssa).
   - Voitetusta taistelusta 2–10 romumetallia (`TurnManager.AwardCombatVictoryScrap()`).
   - Toinen kohtaaminen samalla alueella tarvitsee `roomKey`-arvon `DungeonRoomController`-ohjaimeen.
6. **Pomot (näyttönimet kaikkialla; kirjeissä "the old commander", "the Witch", "the King"):**
   - **Cursed Commander** (Zone 3): 54 HP, 16 AC. Baldurin vihje ruostuneista säärisuojista (`CommanderArmorWeakened`) = −2 AC koko taistelun ajan kaikille sankareille; lippu ei kulu, pätee myös uusintayrityksessä.
   - **Shadow Witch** (Zone 4, nainen): 44 HP, 13 AC. Varjosalama 4 ruutua (2d4+3), yksi peilikuva, teleporttaa enintään joka toinen vuoro.
   - **The Gargoyle King** (Zone 7): 66 HP, 15 AC. Vaihe 2 (50 % HP) *Stone Form* +2 AC ja kivettävä katse (DC 13 CON). Othelian sormus rikkoo kivihaarniskan 2 vuoroksi.
7. **Maailmankartta (Build Settings -järjestys, bannerin nimi suluissa):**
   - `Zone_1_VillageAndCellar` (Oakhaven Village / Village Wine Cellar): kylä, paja, taverna, kellari.
   - `Zone_2_ForestPath` (Whispering Woods): laajennettu x -40..80, z -95..55, mökki ja lampi (`Forest_Expansion`). Taistelut: zombi, sudet, väijytys (luurankojousimies + kultisti). Lukittu portti länsiseinällä (vain varas).
   - `Zone_2.5_SecretForestPath` (**Hidden Trail**): salapolku Forestin portilta Libraryn sivuovelle (`Door_Library_SecretSide`); varkaan arkku.
   - `Zone_3_CastleCourtyard` (Castle Courtyard): 1. pomo Cursed Commander.
   - `Zone_4_Library` (Grand Archives): 2. pomo Shadow Witch, velhon kirjahyllytehtävä.
   - `Zone_5_CastleHall` (The Great Hall): turva-alue, `SavePoint.cs` -riimukivialttari, Pip the Peddler (aave), soturin laatikot.
   - `Zone_6_Tower` (Treasure Tower): Tornin haaste (kolme riimupilaria SUN–MOON–STAR, painelaattaloukut DEX DC 12, mimic-vartija) avaa Othelian sormuksen ja +30 Max HP Giant's Elixirin.
   - `Zone_7_ThroneRoom` (The Throne Room): 3. pomo The Gargoyle King.
   - Linnan alueiden 3–7 uloskäynnit ovat puisia `SwingDoor`-ovia (`Assets/PREFABS/Doors`, aukeavat 7 m päästä). Jokainen alueen vaihtava `DoorTeleporter` rakentaa ajossa "To <zone>" -kyltin (`ZoneExitSign`).
8. **Talous:**
   - **Seppä Baldur:** romu 1 = 5 kultaa (`ShopManager.SCRAP_TO_GOLD_RATE`, muutettu 6.10.2026). Small Health Potion 15g, Greater Health Potion 25g, Poison Vial 30g, Rune of Fate 75g. Sharpened Blade (+1 DMG) ja Runic Armor (+1 AC) enintään +3, hinnat 50 / 75 / 100g.
   - Baldurin pykälät lasketaan `BaldurUpgrade:`-tarinalipuista, joten tehtävistä saatu +DMG ei kuluta niitä.
   - **Pip the Peddler** (Castle Hall, `ScriptedNpc.MerchantStock`): Scroll of Warding 60g, Scroll of Embers 50g, Rune of Fate 60g, Greater Health Potion 20g.
   - Uudet itemit menevät `Assets/Resources/Items`-kansioon (`InventoryManager` hakee sieltä varalla).
9. **Musiikki (`MusicManager`):** kylällä oma teema, muut alueet `Castle_adventure_song`, pomoilla omat taistelukappaleet (Kingillä kaksi vaihetta). Alueen oma tutkimuskappale: `Assets/Resources/Music/Explore_<Forest|Courtyard|Library|CastleHall|Tower|CrownHall>.mp3` tai MusicManagerin per-zone-slotit Zone 1:ssä (tällä hetkellä tyhjiä).
10. **D&D-säännöt (6.10.2026):**
   - Taitobonus +2 (taso 5: +3) osumaan, luokan taitoihin ja kahteen pelastusheittoon; DC = 8 + taito + pääominaisuus (`HeroAttributes`). Varkaalla Expertise (Stealth, Sleight of Hand).
   - Viholliset heittävät pelastusheitot (`EnemyUnit.SaveProfile`, `CombatRules.TryGetSave`): Fireball DEX ½, War Cry STR ½, Arcane Chains STR ½, Poison Cloud CON ½, Smoke Bomb CON.
   - Vuorossa liike + toiminto + bonustoiminto (`ActionEconomy`: Iron Will, Blink, Shadow Step, juoma, myrkkypullo; varkaan Dash/Disengage) ja reaktio (vapaalyönti pakenevaan, ei velholla). Toimintorivi: Dash, Dodge, Disengage, käärö (toiminto), myrkky.
   - Death's Door kerran taistelussa (DC 10, ei nyrkkitappelussa), HP joka tasolta (6/5/4), yllätys: hiiviminen (V) → Stealth, väijytys → Perception DC 13.
   - Antautuminen vain kultistilla (25 %, WIS DC 12) ja Noidalla (20 %; Isolden sormus = aina) → `SurrenderDialogues`. Inspiraatio hyvistä teoista (`HeroicInspiration`). Evil ending: kruunu (`ThroneRoomEnding.EvilEndingFlag`).
   - Taikaesineet (`MagicItems`, `Resources/Items`, `ItemType.Wondrous`) ja tutkittavat kohdat (`InvestigationSpawner`) sijoitetaan ajossa, ei skeneihin. Hahmokortti: C.

---

## 4. UNITY 6 & WEBGL -ARKKITEHTUURISÄÄNNÖT

1. **Syöte (`Active Input Handling = Both`):**
   - Player Settingsissä `Both` (`activeInputHandler: 2`).
   - `EventSystem`-objektissa `InputSystemUIInputModule`.
   - Pelin syöte luetaan staattisesta `GameInput.cs`-sillasta (New Input System, Legacy-varapolku, allokaatioton pollaus). Pelitila vaihdetaan `GameManager.Instance.SetMode(GamePlayMode.Exploration / Combat)`. Erillisiä Exploration/Combat-action mapeja ei ole (assetissa vain `Player` ja `UI`).
   - Pikanäppäimet: `Q` pika-HP-juoma, `I` laukku (`InventoryUI`, vain taistelun ja keskustelun ulkopuolella), `M` kartta, `J` tehtäväpäiväkirja, `Esc` sulkee/tauko, `F3` kohtaamisten debug-overlay (`EncounterDebugOverlay`, vain Editor ja development build).
2. **WebGL Air-Gap & Zero-GC Muistisäännöt:**
   - Älä koskaan tee `System.IO`-kovalevykutsuja tai ulkoisia HTTP-kutsuja pelisilmukassa.
   - Tallennus: JSON `PlayerPrefs`-muistiin avaimella `CastleOfDice_SaveData` (`SaveSystem.cs`). Tarinavalinnat ja edistyminen ovat `StoryFlags`-lippuja, jotka tallentuvat mukana.
   - Äänentoisto: aina 12x AudioSource -poolin kautta (`SFXManager.DefaultPoolSize`). Ei jatkuvaa `Instantiate`/`Destroy`-kutsua.
   - Ei LINQ-hakuja (`.Where()`, `.Select()`) eikä jatkuvaa merkkijonokonkatenaatiota `Update()`-metodeissa; HUD vertaa kokonaislukuja ja rakentaa tekstin vain muutoksessa.
3. **Layerit:** omia layereita 6–9 ei ole määritelty (`TagManager.asset`). Ajossa rakennettavat koristeet (`ZoneExitSign`, `PickupBeacon`) laitetaan layeriin 2 (Ignore Raycast), jotta ne eivät vie klikkauksia.
4. **Persistent Managers (`DontDestroyOnLoad`):** `GameManager`, `SceneLoader`, `MusicManager`, `SFXManager`, `PlayerProgressionManager`, `InventoryManager` sekä `PlayerHUD`, `PauseMenuUI`, `QuestJournalUI`. Jokaisessa skenessä on `Managers`-objekti; tuplat poistetaan `ManagerDuplicates`-apurilla.
5. **UI Canvas (1920x1080 Match 0.5):**
   - `Hero_Status_Card` (vasen ylä): crest, HP, kulta, romu, pika-HP-juoma [Q], `Hero_Stats_Panel`.
   - `Zone_Indicator_Banner` (keskellä ylhäällä) ja sen alla taistelun `Enemy_Stats_Strip` (`CombatStatsHUD`).
   - `Quest_Tracker_Card` (oikea ylä, `QuestHUDUIController.cs`), sen alla `CombatLogPanel` (taisteluloki, näkyy hetken taistelun jälkeen).
   - `CombatActionBar` (keskellä alhaalla): 4 kykyä (+5. tasolla 4) ja End Turn -nappi tiimalasilla; nappi sykkii, kun muuta tehtävää ei ole.
   - `DiceModalPanel` (keskellä): D20-heittoikkuna. HUDissa myös BAG (I)- ja MAP (M) -napit.

---

## 5. TESTIT

- **EditMode** (`Assets/Tests/EditMode`): lähtötaso 6.10.2026 **869 / 885 läpi**. Hylkäyksistä 11 on tunnettuja `UnityCliBridge`-pakettitestejä (auth token, Addressables), jotka kaatuvat aina batch-tilassa; loput 5 ovat ohitettuja. Pelin omien testien pitää mennä läpi.
- **PlayMode** (`Assets/Tests/PlayMode`): `MainPathSmokeTests` pelaa pääreitin jokaisella luokalla (n. 5 min / luokka). Pääreitin testit riippuvat nopista, joten satunnainen hylkäys ei yksin todista bugia; aja uudelleen ennen johtopäätöksiä. Batch-PlayMode-testeissä käytä `yield return null`, ei `WaitForEndOfFrame` (ei laukea `-batchmode`-tilassa).

---

## 6. C#-KOODAUSKAAVAT

### D20-Heitot
```csharp
DiceResult result = DiceSystem.RollD20(bonus: player.PrimaryAttributeBonus, targetDC: enemy.ArmorClass);
DiceResult advResult = DiceSystem.RollD20(bonus: player.PrimaryAttributeBonus, targetDC: 14, advantage: AdvantageType.Advantage);
```

### Kyvyn animaatio ja ääni
```csharp
// AbilityExecutor: kyvyn oma trigger (BaseAbilityID), muuten yleinen loitsu tai lyönti
if (!caster.TrySetAnimatorTrigger(ability.BaseAbilityID))
    caster.UnitAnimator.SetTrigger(isSpell ? "CastSpell" : "Attack");
SFXManager.Instance.PlaySFX(SFXClipType.SwordHit, transform.position);
```

### Tarinaliput
```csharp
StoryFlags.Set("CommanderArmorWeakened");   // tallentuu seikkailun mukana
if (ClassQuests.IsRewarded(ClassQuests.For(hero))) { /* ... */ }
```
