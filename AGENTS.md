# KNOWLEDGE BASE DIRECTIVE (Grounding)

## Strict NotebookLM Policy
- Kaikkien alagenttien ja orkestraattorin AINOA sallittu ulkoinen tietolähde on seuraava muistikirja:
  - **Notebook ID:** `ab6acec3-5413-4df2-8352-5fd173af3806`
  - **Resource Name:** `notebooks/ab6acec3-5413-4df2-8352-5fd173af3806`

## Rules for Tool Calls:
1. **Älä koskaan kutsu `list_notebooks` -työkalua.** Kohdemuistikirja on jo tiedossa ja lukittu.
2. Kun kutsut `retrieve_relevant_chunks`- tai `query_notebook` -työkalua:
   - Käytä parametrinä AINA yllä määriteltyä Notebook ID:tä: `ab6acec3-5413-4df2-8352-5fd173af3806`.
   - Muiden muistikirjojen hakeminen tai lukeminen on ehdottomasti kielletty.
3. Jos pyydettyä tietoa ei löydy tästä muistikirjasta, kysy käyttäjältä tarkennusta äläkä arvaile (hallusinoi) puuttuvia sääntöjä tai arkkitehtuuria.

# 🤖 AI Agent & MCP Developer Guide: Castle of Dice

Tämä ohjeisto on tarkoitettu ulkoisille tekoälyagenteille (Cursor, Claude Code, Windsurf, ChatGPT, MCP-palvelimet) ja kehittäjille, jotka lukevat, muokkaavat tai laajentavat **Castle of Dice** -pelin C#-koodikantaa.

---

## 1. Arkkitehtuuriset Perussäännöt (Architectural Directives)

1. **Singleton-malli persistentille ohjaimille:**
   Käytä aina laiskaa tai `Awake()`-alustettua Singleton-mallia pääohjaimissa (`GameManager`, `MusicManager`, `SFXManager`, `SceneLoader`, `PlayerProgressionManager`):
   ```csharp
   public static GameManager Instance { get; private set; }
   private void Awake() {
       if (Instance != null && Instance != this) { Destroy(gameObject); return; }
       Instance = this;
       DontDestroyOnLoad(gameObject);
   }
   ```
2. **WebGL Air-Gap & Muistisäännöt:**
   - Älä koskaan kirjoita `System.IO`-pohjaisia kovalevykutsuja tai ulkoisia HTTP-rajapintapyyntöjä suoraan pelisilmukassa.
   - Käytä AudioSource-poolausta (`SFXManager.cs`) eipä-roskienkeruun (Zero-GC) saavuttamiseksi selaimessa.
3. **New Input System -Tilanvaihto:**
   - Kytke syötekartat (`Exploration` vs `Combat`) puhtaasti pelitilan vaihtuessa:
   ```csharp
   inputActions.Exploration.Disable();
   inputActions.Combat.Enable();
   ```

---

## 2. D20-Moottorin Kutsukaavat

Aina kun suoritat osuma- tai taitoheiton koodissa, käytä `DiceSystem`-luokan staattisia metodeja:

```csharp
// Standardi D20-heitto
DiceResult result = DiceSystem.RollD20(modifier: player.AttackBonus, targetDC: enemy.ArmorClass);

// Advantage / Disadvantage -heitto
DiceResult advResult = DiceSystem.RollAdvantage(modifier: player.DexterityBonus, targetDC: 14);

// Tuloksen tarkistus
if (result.isNatural20) {
    // Critical Hit (Tuplavahinko)
} else if (result.isSuccess) {
    // Normaali osuma
} else {
    // Huti -> Tarkista RuneOfRerollController!
}
```

---

## 3. UI-Korttien & HUD-Komponenttien Kytkentä

1. **`Hero_Status_Card` (Vasen yläreuna):**
   - Päivitetään tapahtumapohjaisesti aina kun `PlayerUnit.OnStatsChanged` laukeaa.
2. **`Quest_Tracker_Card` (Oikea yläreuna):**
   - Ohjataan `QuestHUDUIController.cs`-skriptillä.
   - Tila-arvojen muuttuessa kutsu `QuestHUDUIController.Instance.RefreshQuestList()`.

---

## 4. Animaatio- & Äänikutsut

Käytä aina vakioituja animaatiotriggereitä ja äänipoolin kutsuja:

```csharp
// Animaatio
animator.SetTrigger("Attack");

// Viivästetty vahinko (Coroutine)
yield return new WaitForSeconds(0.35f);
target.TakeDamage(damageAmount);

// Ääni
SFXManager.Instance.PlaySFX(SFXClipType.SwordHit, transform.position);
```

---

*Pidä tämä ohjeisto mukana AI-kontekstissa koodia luotaessa.*

# AGENT SKILLS & SPECIALIZATIONS

## Skill 1: Unity 6 WebGL Performance & Zero-GC
- **Ei roskienkeruuta (Zero-GC):**
  - Älä käytä `LINQ`-kyselyitä (`.Where()`, `.Select()`) `Update()`- tai `FixedUpdate()`-metodeissa.
  - Vältä jatkuvaa string-konkatenaatiota silmukoissa; käytä esialustettuja merkkijonoja tai `StringBuilderiä`.
- **Muistinhallinta:**
  - Kaikki toistuvat objektit (ammukset, tekstit, äänet) TÄYTYY toteuttaa olio-poolauksella (`Object Pool`), ei jatkuvaa `Instantiate` / `Destroy` -kutsumista.

## Skill 2: Unity New Input System Expert
- Älä koskaan kirjoita vanhaa koodia tyyliin `Input.GetKeyDown(KeyCode.Space)`.
- Käytä aina `com.unity.inputsystem`-paketin `InputActionAsset`-määrityksiä ja Action Mapeja (`Exploration` ja `Combat`).
- Muista aina tilisiirtymissä poistaa vanha kartta käytöstä ennen uuden aktivointia:
  `inputActions.Exploration.Disable(); inputActions.Combat.Enable();`

## Skill 3: D20 Math & Grid Navigation
- Kaikki osumat ja taitotarkistukset suoritetaan kaavalla: `d20 + modifier >= DC`.
- Ruudukon BFS-reitinhaussa (Pathfinding) ei saa käyttää fysiikkasäteitä (`Physics.Raycast`) pelisilmukan aikana, vaan reitit luetaan valmiista `GridTile[,]` -muistimatriisista.

## Skill 4: ScriptableObject Architecture
- Pelin tila ja konfiguraatiot (viholliset, esineet, loitsut) eivät saa olla kovakoodattuina MonoBehavioreissa, vaan ne määritellään `ScriptableObject`-tietueina (`PlayerDataSO`, `EnemyDataSO`, `LootTableSO`).

# 🛠️ UNITY EDITOR CLI INTEGRATION

Projektin juurihakemistossa on suoritettava työkalu `.\unity-cli.exe`, jolla on aktiivinen yhteys taustalla auki olevaan Unity Editoriin.

## Agent Directives for Unity Control:
1. **Virheiden tarkistus koodauksen jälkeen:**
   - Aina kun luot tai muokkaat C#-skriptejä, tarkista Unityn konsoli ajamalla terminaalissa:
     `.\unity-cli.exe tool call read_console`
   - Jos lokissa on virheitä (`error`), korjaa ne välittömästi.
2. **Komponenttien ja skenen tarkastus (TÄRKEÄ SÄÄNTÖ):**
   - ÄLÄ KOSKAAN käytä vanhentunutta `raw get_scene_hierarchy` -komentoa (se tuottaa `UNKNOWN_COMMAND` -virheen).
   - Käytä AINA työkalua `find_by_component` tai `get_hierarchy`.
   - **PowerShell-parametrien invariantti:** Älä koskaan syötä monimutkaista JSONia suoraan `--json`-parametrina komentorivillä, sillä PowerShell rikkoo lainausmerkit. Kirjoita parametrit aina väliaikaistiedostoon ja käytä `--params-file`:
     ```powershell
     Set-Content -Path "test_param.json" -Value '{"componentType": "QuestHUDUIController"}'
     .\unity-cli.exe tool call find_by_component --params-file "test_param.json"
     ```
3. **Testien ja editoritilan hallinta:**
   - Työkalulista: `.\unity-cli.exe tool list`
   - Testit: `.\unity-cli.exe tool call run_tests`
   - Resurssipäivitys: `.\unity-cli.exe tool call refresh_assets`