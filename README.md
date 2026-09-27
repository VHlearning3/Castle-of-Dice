# 🎲 Castle of Dice - Unity 6 WebGL Taktinen D20-Roolipeli

**Castle of Dice** on verkkoselaimessa (WebGL) toimiva, suoraviivaistettu 3D-pöytäroolipeli, joka yhdistää taktisen vuoropohjaisen ruudukko-taistelun, d20-noppamekaniikan, matalan kynnyksen tutkinnan sekä rikaan tarinallisen hahmonkehityksen.

Peli on kehitetty **Unity 6 (6000.3.14f1 LTS)** -moottorille ja se on optimoitu saumattomaan selaintoteutukseen ilman suorituskykynykäyksiä tai muistivuotoja.

---

## 🏛️ Pelin Ydinkonsepti & Ominaisuudet

- **Aito D20-sääntöjärjestelmä:** Kaikki toiminnot (hyökkäykset, tiirikointi, puhuttelutestit) pohjautuvat kaavaan `d20 + modifier ≥ AC / DC`.
- **Älykäs Nopanheittopysäytys & Reroll-kääröt:** Rutiiniheitot etenevät vauhdikkaasti, mutta jos pelaajalla on harvinainen *Uudelleenheitto-käärö* (Rune of Reroll), peli pysähtyy antamaan taktisen miettimistauon.
- **3 Uniikkia Sankariluokkaa:**
  - 🛡️ **Soturi (Sir Roland Rautakoura):** Raskas etulinjan taistelija, korkea HP & AC.
  * 🔮 **Velho (Oppinut Elira):** Taktinen loitsija, 3x3 Tulipallo-aluevahinko ja Mana-kilpi.
  * 🗡️ **Varas (Varjo-Corvo):** Hiipijä ja tiirikoinnin ammattilainen, pystyy avaamaan salareittejä ja ohittamaan pomoja.
- **Epälineaarinen Kartta (`clear_map.png`):**
  - Kivenkolon kylä (Oakhaven) & Viinikellari
  - Metsäpolku & Salaportti (`PUZZLE + GATE`)
  - Keskushalli (Turvallinen lepopaikka & `SavePoint`)
  - Aarretorni (Legendaarinen +30 Max HP Eliksiiri)
  - 3 Uniikkia Pomoa: Kirottu Komentaja, Varjomaagi Malakor ja Kivettymiskuningas.
- **Ergonominen HUD-UI:**
  - **Hero_Status_Card** (Vasen yläreuna): HP, Kulta, Romumetalli (2–10 kpl/taistelu) ja HP-juomat.
  - **Quest_Tracker_Card** (Oikea yläreuna): Minimoitava tehtävälista suorilla X/X-laskureilla.

---

## 📁 Projektin Hakemistorakenne

```
Assets/
├── Core/
├── Audio/
│   ├── MusicClips/         # Zone- ja Pomo-musiikit
│   └── SFX/                # Isku-, noppa- ja UI-ääniefektit
├── Data/
│   ├── Characters/         # CharacterDataSO & CharacterSkillSO
│   ├── Enemies/            # EnemyDataSO & EnemyDataPresets
│   ├── Items/              # LootTableSO & EnvironmentHazardSO
│   └── Music/              # ZoneMusicSO-säiliöt
├── Prefabs/
│   ├── UI/                 # QuestEntryPrefab, DiceModal, StatusCard
│   ├── Grid/               # GridTile, EnvironmentHazards
│   └── Characters/         # PlayerUnit & EnemyUnit Prefabit
├── Scenes/
│   ├── Zone_Village.unity
│   ├── Zone_ForestPath.unity
│   ├── Zone_CastleLobby.unity
│   ├── Zone_CastleLibrary.unity
│   └── Zone_ThroneRoom.unity
└── Scripts/                # Kaikki 31+ C#-tuotantoskriptiä
```

---

## ⚙️ Tekninen Vaatimusmäärittely

| Parameter | Specification |
| :--- | :--- |
| **Unity Version** | Unity 6 (6000.3.14f1 LTS) |
| **Target Platform** | WebGL (WebAssembly) |
| **Render Pipeline** | Universal Render Pipeline (URP) Low-Poly |
| **Input System** | Unity New Input System (`com.unity.inputsystem`) |
| **Reference Resolution** | 1920x1080 (Scale With Screen Size, Match = 0.5) |
| **Controls** | Hiiri & Näppäimistö (WASD / Nuolet, E/Space, Klikkaus) |

---

## 📜 Dokumentaatio & Tiedostot

- `CastleOfDice_MasterSpec.md`: Koko järjestelmän pääarkkitehtuuridokumentti.
- `UNITY_SETUP_GUIDE.md`: Vaiheittainen Unity 6 -editorin asennus- ja kytkentäopas.
- `GDD_CastleOfDice.md`: Täydellinen Game Design Document & D20-tasapainospesifikaatio.
- `AI_AGENT_PROMPT_GUIDE.md`: Ohjeistus ulkoisille AI-agenteille (Cursor/Claude/MCP) koodikannan lukemiseen ja kehittämiseen.

---

*Lisensoitu Castle of Dice -kehitystiimille 2026.*