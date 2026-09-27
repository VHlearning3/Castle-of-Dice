# 🛠️ Unity 6 WebGL Asennus- ja Kytkentäopas

Tämä opas neuvoo vaihe vaiheelta, miten **Castle of Dice** -pelin C#-skriptit, Canvas-elementit, prefabit ja alue-skenet kytketään toimintakuntoon **Unity 6 (6000.3.14f1 LTS)** -editorissa.

---

## Vaihe 1: Projektin Alustus & Paketit

1. Avaa Unity Hub ja luo uusi projekti valitsemalla **3D (URP)**.
2. Varmista, että versio on **6000.3.14f1 LTS** (tai muu Unity 6 LTS -versio).
3. Avaa **Window -> Package Manager** ja varmista seuraavat paketit:
   - **Input System** (`com.unity.inputsystem`): Asenna ja aktivoi.
   - **TextMeshPro** (`com.unity.textmeshpro`): Importtaa Essential Resources.
4. Avaa **Edit -> Project Settings -> Player -> WebGL tab**:
   - **Color Space:** Linear
   - **Publishing Settings:** Enable Exceptions = Full, Compression Format = Brotli / Gzip.

---

## Vaihe 2: Tagit, Layerit & Input System

### 1. Layerit (Project Settings -> Tags and Layers)
Määritä seuraavat layerit:
- `Layer 6`: `GridTile`
- `Layer 7`: `Obstacle` (Seinät, pylväät, kivenjärkäleet)
- `Layer 8`: `Interactable` (Aarrearkut, kukkaset, sormus, NPC:t)
- `Layer 9`: `Unit` (Pelaaja ja viholliset)

### 2. Input System Asset (`CastleOfDiceInput.inputactions`)
Luo projektikansioon uusi Input Actions -tiedosto `CastleOfDiceInput`:
- **Action Map: `Exploration`**
  - `Move` (Value / Vector2): WASD, Nuolinäppäimet
  - `Interact` (Button): `E`, `Space`, Hiiren vasen painike
  - `ToggleMenu` (Button): `Escape`
- **Action Map: `Combat`**
  - `SelectTile` (Button): Hiiren vasen painike
  - `Skill1`..`Skill4` (Button): Näppäimet `1`, `2`, `3`, `4`

---

## Vaihe 3: Pääohjaimet & Persistent Managers (DontDestroyOnLoad)

Luo ensimmäiseen skeneen (`Zone_Village.unity`) tyhjä GameObject nimeltä `_CoreManagers`:

```text
_CoreManagers
├── GameManager (Script: GameManager.cs)
├── SceneLoader (Script: SceneLoader.cs + LoadingCanvasGroup)
├── MusicManager (Script: MusicManager.cs + ZoneMusicSO Database)
├── SFXManager (Script: SFXManager.cs + AudioSource Pool)
└── PlayerProgressionManager (Script: PlayerProgressionManager.cs)
```

### Kytkennät Inspectorissa:
1. **`SceneLoader.cs`**:
   - Drag `LoadingCanvasGroup` kohtaan **Loading Canvas Group**.
   - Drag `ProgressBarSlider` ja `ProgressPercentageText` omille kentilleen.
2. **`MusicManager.cs`**:
   - Luo `ZoneMusicSO`-objektit 6 alueelle ja vedä ne `Zone Music Database` -listaan.
3. **`SFXManager.cs`**:
   - Aseta AudioSource-poolin kooksi `12`. Drag äänitehosteet Inspector-listoihin.

---

## Vaihe 4: UI Canvas Setup (`PlayerHUD`)

Luo Canvas (1920x1080, Scale With Screen Size, Match = 0.5):

```text
PlayerHUD (Canvas)
├── Hero_Status_Card (Anchor: Top-Left [0, 1])
│   ├── HealthText & Slider
│   ├── GoldText ("120g")
│   ├── ScrapMetalText ("8 kpl")
│   └── PotionText ("3 kpl")
│
├── Quest_Tracker_Card (Anchor: Top-Right [1, 1], Script: QuestHUDUIController)
│   ├── HeaderTitleText ("TEHTÄVÄT")
│   ├── MinimizeButton ("[ − ]")
│   └── QuestListContainer (Vertical Layout Group)
│       └── [Prefabs: QuestEntryUI]
│
└── DiceModalPanel (Anchor: Center [0.5, 0.5], Script: RuneOfRerollController)
    ├── DiceSpinNumberText ("17")
    ├── ResultBannerText ("ONNISTUMINEN!")
    ├── ContinueButton ("Jatka")
    └── RerollButton ("📜 Käytä Uudelleenheitto-kääröä")
```

---

## Vaihe 5: Alueskenet & Siirtymäportit

1. Lisää kaikki 5–6 alueskeneä **File -> Build Settings -> Scenes In Build** -listalle:
   - `0: Zone_Village`
   - `1: Zone_ForestPath`
   - `2: Zone_CastleLobby`
   - `3: Zone_CastleLibrary`
   - `4: Zone_ThroneRoom`
2. Sijoita jokaisen skenen siirtymäovelle `DoorTeleporter`-objekti, joka kutsuu latausta:
   ```csharp
   SceneLoader.Instance.LoadSceneAsync("Zone_CastleLobby");
   ```

---

## Vaihe 6: Testaus & WebGL Build

1. Paina **Play** Unity Editorissa ja testaa:
   - Hahmon valinta päävalikossa.
   - Rotta-taistelu kellarissa ja d20-nopparullaus.
   - NPC-puhuttelu ja `50g + 2 HP potion` -palkinnon saaminen.
   - Alueenvaihto ja asynkroninen lataus ruutuvinkkeineen.
2. Suorita WebGL-käännös: **File -> Build Settings -> WebGL -> Build**.