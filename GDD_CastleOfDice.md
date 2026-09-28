# 📜 Game Design Document (GDD): Castle of Dice

**Versio:** 2.6  
**Moottori & Kohde:** Unity 6 (6000.3.14f1 LTS) WebGL (60 FPS, WebAssembly, URP 17.3.0)  
**Syötejärjestelmä:** Dual Input Handling (New Input System 1.19.0 + Legacy Input)  
**Tyylilaji:** Taktinen D20-Pöytäroolipeli / Dungeon Crawler  

---

## 1. Pelisuunnittelun Pääpilarit & Taktinen Areenamuotoilu

1. **Aito D20-tuntuma ilman kitkaa:** Pelissä käytetään D&D 5e -henkistä kaavaa `d20 + modifier ≥ DC / AC`. Arjen rutiiniheitot automatisoidaan nopsaan tahtiin, mutta kohtalokkaat heitot ja taitoheitot pysäytetään nopanheittoruutuun (`DiceUIController`). *Rune of Reroll* -riimurulla mahdollistaa heiton uusimisen epäonnistumisen hetkellä.
2. **12x12 Taktinen Ruudukko & Soolosankarin Tempo:** Taistelukentät luodaan koolla **12x12 ruutua** (144 solua, `tileSize = 1.6f`), mikä tarjoaa tilaa etäisyyden pitämiselle (kiting), alue-loitsuille ja ansoille.
3. **Pelaajan Taktinen Vapaus (Solo Hero Agency):** Seikkailua pelataan yhdellä sankarilla kerrallaan. Vihollistiheys pidetään napakkana (1 eliittivihollinen tai max 2 vihollista per taistelu), jotta vuorotahti säilyy nopeatempoisena.
4. **Palkitseva Seikkailusilmukka (Core Loop):**
   ```text
   Kivenkolon Kylä (Paja & Tehtävät) ──► Linnan Siipien Tutkiminen ──► 12x12 D20-Taistelu & Salaisuudet ──► Pomon Voitto & Milestone Level-Up
   ```

---

## 2. Sankariluokat & Kyvyt

Jokainen luokka on suunniteltu omavaraiseksi soolotaistelijaksi, jolla on välineet vahingontekoon, aluehallintaan (CC/työntö/teleportti) ja suojautumiseen:

### 🛡️ Soturi (Sir Roland Rautakoura)
- **Tilastot:** 30 HP, 14 AC, Liike 4 ruutua. Pääattribuutti: Voima (+3).
- **Kyvyt:**
  1. *Miekansivallus (Sword Cleave):* Lähitaistelu `1d8 + 3` vahinkoa pääkohteelle + puolet vahingosta viereiselle viholliselle.
  2. *Kilpimuuri (Shield Wall):* Nostaa puolustusta +4 AC vuoron ajaksi. Tekee automaattisen vastaiskun (`1d6`), jos vihollisen hyökkäys heittää ohi.
  3. *Sotahuuto (War Cry):* 3x3 shockwave-työntö. Työntää viereisiä vihollisia 1–2 ruutua taaksepäin ja tekee `1d4 + 3` vahinkoa.
  4. *Rautainen tahto (Iron Will):* Itseparannus (palauttaa 30 % Max HP:sta) ja poistaa kaikki negatiiviset tilat (debuffit).

### 🔮 Velho (Oppinut Elira)
- **Tilastot:** 20 HP, 12 AC, Liike 3 ruutua. Pääattribuutti: Älykkyys (+3).
- **Kyvyt:**
  1. *Jäänsäde (Frostbite Ray):* Kantama 4 ruutua, `1d6 + 3` taikavahinkoa + puolittaa kohteen liikkumisnopeuden 1 vuoroksi.
  2. *Tulipallo (Fireball):* Kantama 4 ruutua, 3x3 aluevahinko, `2d6` vahinkoa kaikille alueella oleville.
  3. *Mana-kilpi (Mana Shield):* Absorboi ja kumoaa seuraavan tulevan hyökkäyksen vahingon 100-prosenttisesti (1 charge).
  4. *Teleportti (Blink):* Siirtyy välittömästi 5–7 ruutua ilman vastahyökkäyksiä.

### 🗡️ Varas (Varjo-Corvo)
- **Tilastot:** 25 HP, 13 AC, Liike 5 ruutua. Pääattribuutti: Ketteryys (+3).
- **Kyvyt:**
  1. *Myrkkytikari (Poison Dagger):* `1d4 + 3` välitön vahinko + `1d6` myrkkyvahinkoa/vuoro 2 vuoron ajan.
  2. *Selkäänpuukotus (Backstab):* Advantage-heitto (2d20). Tekee `2d6 + 3` vahinkoa (kaksinkertainen vahinko, jos kohde on sokea tai Varas käytti Varjoaskelta).
  3. *Savupommi (Smoke Bomb):* Kantama 3, 3x3 alue. Sokeuttaa viholliset 1 vuoroksi (aiheuttaa vihollisille Disadvantage-haitan hyökkäyksiin).
  4. *Varjoaskel (Shadow Step):* Teleporttaa Varkaan 3 ruudun päähän tyhjään ruutuun JA antaa automaattisen Advantage-edun (2d20) seuraavaan hyökkäykseen.  
  *(Huom: Tiirikointi on Varkaan passiivinen maastotaito `LockpickInteraction.cs`-skriptissä, ei taistelupalkin aktiivinen kyky).*

---

## 3. Maailmankartta & Pelialueet (7 Zone-Aluetta)

Maailma on jaettu 7 toisiinsa kytkettyyn Unity-skeneen:

```text
Zone_1_VillageAndCellar ──► Zone_2_ForestPath ──► Zone_3_CastleCourtyard
                                 │ (DC 13 Salaportti)
                                 ▼
Zone_7_ThroneRoom ◄── Zone_6_Tower ◄── Zone_5_CastleHall ◄── Zone_4_Library
```

1. **`Zone_1_VillageAndCellar`:** Aloituspiste (`START`), Oakhavenin kylä, Sepän paja, Taverna ja Viinikellarin tuholaistaistelu.
2. **`Zone_2_ForestPath`:** Metsäpolku ja tirikoitava salaportti (`PUZZLE + GATE`).
3. **`Zone_3_CastleCourtyard`:** Linnan alapiha & 1. Pomo: **Kirottu Komentaja**.
4. **`Zone_4_Library`:** Salatieteen siipi & 2. Pomo: **Varjomaagi Malakor**.
5. **`Zone_5_CastleHall`:** Keskushalli / `HALL` (Turvaalue, `SavePoint.cs` Ruunikivialttari – palauttaa HP:n 100 % ja tallentaa pelin).
6. **`Zone_6_Tower`:** Piilotettu Aarretorni (Sisältää Othelian sormuksen ja *Jättiläisen eliksiirin*: **+30 Max HP**).
7. **`Zone_7_ThroneRoom`:** Kruununsali & 3. Pomo: **Kivettymiskuningas**.

---

## 4. Milestone-Tasonnousujärjestelmä

Tasonnousu perustuu seikkailun merkkipaaluihin ja linnan siipien läpäisyyn:

- **Level 1 (Aloitus):** Kylä, kellari ja metsäpolku.
- **Level 2 (Linnan Veteraani):** Voittamalla Kirottu Komentaja tai tirikoimalla Metsäpolun salaportti (DC 13).
- **Level 3 (Max Level - Arkaaninen Murskaaja):** Voittamalla Varjomaagi Malakor Kirjastossa.

### Tasonnousun Valinnat (`LevelUpUIController.cs`):
1. **Sankarin Sitkeys:** +5 Max HP & Täysparannus (Full Heal).
2. **Ominaisuusbonuksen Kasvu:** +1 Pääattribuuttiin (nostaa osumatarkkuutta & vahinkoa +1 pykälällä).
3. **Kyvyn Mahdistaminen:** Päivittää yhden valitun luokkakyvyn Rank 2 -tasolle (+3 lisäteho / lisävaikutus).

---

## 5. Talous & Palkintomekaniikat

- **Romumetalli (Scrap Metal):** Taistelun voittamisesta saa aina **2–10 kpl romumetallia** (`TurnManager.AwardCombatVictoryScrap()`).
- **Seppä Baldurin Paja (`ShopManager.cs`):**
  - 1 Romumetalli = 10 Kultaa.
  - Pieni terveysjuoma = 25 Kultaa (Palauttaa 15 HP).
  - Teroitettu miekka = 60 Kultaa (+1 Pysyvä vahinkobonus).
  - Riimukilpi / Haarniska = 100 Kultaa (+1 Pysyvä AC-bonuskaiverrus).
- **Tehtäväsysteemi (`QuestManager.cs`):**
  - Tehtävien suoritus antaa kultaa ja potioneita. Tehtäväneuvotteluissa (Persuasion DC 13) voi neuvotella lisäkultabonuksen (`:bonus`).

---

## 6. Päävastustajat & Taistelua Edeltävät Dialogidebuffit

Jokaisella pomolla on ainutlaatuiset mekaniikat sekä taistelua edeltävä dialogitarkistus, joka palkitsee onnistuneesta roolipelauksesta:

1. **1. Pomo — Kirottu Komentaja (Alapiha):**
   * **Tilastot:** 50 HP, 16 AC. Kutsuu 1 luurangon (`skeletonCount = 1`) 50 % HP:ssa.
   * **Dialogidebuff (`Soldier's Honor | DC 13`):** Onnistuminen asettaa `CommanderArmorWeakened`-tagin, joka alentaa Komentajan AC:ta -2 pykälää (**14 AC**) kahden ensimmäisen taisteluvuoron ajaksi.

2. **2. Pomo — Varjomaagi Malakor (Kirjasto):**
   * **Tilastot:** 40 HP, 13 AC. Teleporttaa vahingosta ja luo 1 peilikuvakloonin (`decoyCount = 1`).
   * **Dialogidebuff (`Arcane Heresy | DC 14`):** Onnistuminen asettaa `ArcaneHeresy`-tagin, joka paljastaa aito-Malakorin välittömästi tekstibadgella `[True]`, jottei pelaajan tarvistaisi arvailla peilikuvien joukosta.

3. **3. Pomo — Kivettymiskuningas (Kruununsali):**
   * **Tilastot:** 60 HP, 15 AC (Vaihe 1) ──► 18 AC (Vaihe 2 Stone Form at 50% HP). Maanjäristykset ja taikapeilaus.
   * **Dialogidebuff (`Intimidation | DC 16`):** Onnistuminen asettaa `Intimidated`-tagin, joka heikentää Kuninkaan hyökkäysvoimaa -3 yksikköä kolmen ensimmäisen taisteluvuoron ajaksi.
