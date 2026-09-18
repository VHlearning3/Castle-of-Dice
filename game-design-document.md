# GAME DESIGN DOCUMENT: CASTLE OF DICE (CASTLE OF THE D20)

**Tekijä:** Vili Huhtala  
**Päivämäärä:** 27.08.2026 / Päivitetty 2026  
**Versio:** 1.0 (Unity WebGL Production Blueprint)  

---

## 1. YHTEENVETO & HIGH CONCEPT

### 1.1 High Concept Statement
*Castle of Dice* (työnimeltään *Castle of the D20*) on kevyt, selainpohjainen 3D-seikkailuroolipeli, jossa pelaaja varustautuu turvallisessa kylässä ja etenee vanhan linnan läpi kukistaen pomoja yksinkertaistetulla D20-noppamekaniikalla ja tyylitellyllä low-poly-grafiikalla.

### 1.2 Perustiedot
* **Genre:** Kevyt 3D-roolipeli / Taktinen linnaseikkailu / Vuoropohjainen RPG
* **Alusta:** Web (HTML5 / WebGL), PC (itch.io)
* **Kohdeyleisö:** Pöytäroolipelien ystävät, nopeatempoista pelattavuutta hakevat selainpelaajat, sword and sorcery -fantasian fanit
* **Pelin Premissi:** Seikkailu alkaa Kivenkolon (Oakhaven) rauhallisesta kylästä, josta käsin lähdetään tutkimaan läheistä, varjojen valtaamaa vanhaa kivilinnaa. Suorittamalla kyläläisten sivutehtäviä ja käymällä vaihtokauppaa pelaaja parantaa varusteitaan, avaa linnan lukittuja siipiä ja etenee huone huoneelta kohti kruununsalin Kivettymiskuningasta.

### 1.3 Uniikit myyntivaltit (USP)
1. **DnD-jännitystä minimaalisella opettelulla:** Aito pöytäroolipelimäinen noppajännitys yhdistettynä selkeään ja välittömään sääntöjärjestelmään.
2. **Saumaton selainpelaus:** Peli toimii suoraan verkkoselaimessa ilman erillistä asennusta tai raskaita latausaikoja.
3. **Nopea ja palkitseva pelisilmukka:** Rytmikäs valmistautumisen, tutkimisen, noppatarkistusten ja taktisen taistelun kokonaisuus.

### 1.4 Pelinsuunnittelun tavoitteet (Design Goals)
* **Design Goal 1 (Tarina & Juoni):** Luoda mielenkiintoinen ja syvällinen tarina maailmasta, jossa hahmojen taustoilla ja valinnoilla on merkitystä.
* **Design Goal 2 (Taktinen taistelu):** Toteuttaa selkeä vuoropohjainen taistelujärjestelmä, jossa jokaisella valinnalla, kyvyllä ja noppaheitolla on aitoa painoarvoa.
* **Design Goal 3 (Maailman elämyksellisyys):** Rakennetun maailman tulee synnyttää aito fantasiaseikkailun tunne, johon pelaaja voi eläytyä.

---

## 2. PELISILMUKKA & MAAILMAN RAKENNE

### 2.1 Pääpelisilmukka (Core Loop)
```
  [1. VALMISTAUDU KYLÄSSÄ] ───► [2. ETENE LINNASSA]
           ▲                             │
           │                             ▼
  [4. KUKISTA SIIPEIN POMOT] ◄─── [3. TAISTELE VUOROPOHJAISESTI]
```
1. **Valmistaudu kylässä:** Osta terveysjuomia, teroitettuja aseita (+1 vahinko) ja panssareita (+1 AC) sepältä. Suorita kyläläisten pikkutehtäviä lisäkullan ja hyötyesineiden saamiseksi.
2. **Etene linnassa:** Liiku 3D-huoneissa, ratkaise ansoja ja esteitä noppatarkistuksilla (`d20 + taitobonus ≥ DC`) sekä löydä salakäytäviä.
3. **Käy vuoropohjaisia taisteluita:** Valitse sankarin 4 kyvystä sopivin toiminto, heitä d20-noppaa osumiseen (`d20 + bonus ≥ AC`) ja hyödynnä taktista sijoittumista.
4. **Kukista siipien pomot:** Etene alapihoilta ja kirjastosta aina linnan huipulle saakka keräten riimuesineitä ja kultasaaliita.

---

### 2.2 Keskuskylä: Kivenkolo (Oakhaven)
Kivenkolo toimii turvallisena lepo- ja kauppapaikkana kivilinnan juurella.

#### NPC-hahmot & Kaupankäynti
* **Seppä & Romukauppias Baldur:**
  * *Rooli:* Karhea ja rehti seppä, joka takoo varusteita raunioiden metallista.
  * *Vaihtokauppa:* 1x Raunioiden romumalmi = 10 Kultaa.
  * *Tavarat:* Pieni terveysjuoma (25 Kultaa), Teroitettu miekka / Vahvistettu sauva (+1 pysyvä vahinko, 60 Kultaa), Riimukilpi / Nahkahaarniska (+1 pysyvä AC, 100 Kultaa).
  * *Vihje:* Kysyttäessä antaa tärkeää tietoa Kirotun Komentajan haarniskan heikkouksista.

#### Sivutehtävät (3 kpl)
| Tehtävä | Antaja | Tavoite | Peruspalkinto | D20-Vaihtoehto / Syventäminen |
| :--- | :--- | :--- | :--- | :--- |
| **1. Viinikellarin tuholaiset** | Barnaby (Majatalon isäntä) | Puhdista kellarista 3 jättirottaa. | 30 Kultaa, 2x Terveysjuoma | **DC 13 (Vakuuttelu):** Nostaa palkkion 45 Kultaan. |
| **2. Kadonnut perintökalleus** | Othelia (Kylänvanhin) | Etsi sukunsa sinettisormus raunioista. | Ilmaisen d20-uudelleenheiton riimukivi | **Lore-kytkös:** Avaa taustatarinaa Kivettymiskuninkaan kirouksesta. |
| **3. Yrttejä parantajalle** | Mirabel (Yrttiparantaja) | Kerää 3 suokukkaa vallihaudalta. | Myrkkypullo (+5 vahinkoa seuraavaan taisteluun) | **DC 10 (Luontotieto):** Vaihtaa palkinnon Suureen Terveysjuomaan. |

---

### 2.3 Vanha Linna (3 Siipeä)
Linna jakautuu kolmeen goottilaiseen teema-alueeseen:

1. **Linnan alapiha & Vartiotorni:**
   * *Viholliset:* Haarniskoidut luurangot, zombi-vartijat.
   * *Tunnelma:* Ruostunutta terästä, kylmää kiveä ja onttoja kypärän kaikuja.
   * *Pääpomo:* **Kirottu Komentaja (Cursed Commander)**.
2. **Kirjasto & Salatieteen siipi:**
   * *Viholliset:* Haamut, leijuvat loitsukirjat.
   * *Tunnelma:* Kuiskaavia varjoja, leijuvia opuksia ja syvän sinistä maagista hämärää.
   * *Pääpomo:* **Varjomaagi Malakor**.
3. **Kruununsali (Pääpomo):**
   * *Viholliset:* Eliittiritarit, kivi-gargoylet.
   * *Tunnelma:* Jylisevää kalliota, muinaisia valtaistuimia ja kiveen hakattua historiaa.
   * *Pääpomo:* **Kivettymiskuningas (Gargoyle King)**.

---

## 3. SÄÄNTÖJÄRJESTELMÄ & MEKANIIKAT

### 3.1 Yksinkertaistettu D20-noppajärjestelmä
Kaikki pelin aktiiviset toiminnot ratkaistaan yhdellä 20-tahoisella nopalla.

$$\text{Tulos} = \text{Noppaheitto (1--20)} + \text{Hahmon taitobonus (+2 \dots +5)} \ge \text{Vaikeusaste (DC / AC)}$$

* **Vaikeusasteet (DC / AC):**
  * *Helppo (DC 10):* Yksinkertaiset esteet tai helpot noppakoetukset.
  * *Keskivaikea (DC 13):* Standardit haasteet, kuten aarrearkun tiirikointi.
  * *Vaikea (DC 16):* Erittäin vaativat esteet tai raskaasti suojatut pomot.
* **Luonnolliset tulokset (Nat 20 & Nat 1):**
  * **Luonnollinen 20 (Nat 20):** Kriittinen onnistuminen. Taistelussa tekee automaattisesti **tuplavahingon**; seikkailussa johtaa automaattiseen täydelliseen onnistumiseen.
  * **Luonnollinen 1 (Nat 1):** Kriittinen epäonnistuminen. Taistelussa vuoro päättyy heti hutiin; seikkailussa toiminta epäonnistuu koomisella tavalla.

---

### 3.2 Vuoropohjainen Taistelujärjestelmä & Ruudukko
* **Tila-automaatti (`TurnManager.cs`):** Vuorot jakautuvat selkeisiin tiloihin (`PlayerTurn`, `EnemyTurn`, `ResolveAbilities`, `Victory`, `Defeat`).
* **Ruudukkoohjaus (`GridController.cs`):** Taistelu tapahtuu ruudukolla. Tiettyjen kykyjen (kuten Velhon *Tulipallo*) kohdalla peli korostaa alueen (esim. 3x3 ruutua) hiiren osoittimen alla.
* **Osumisen ja vahingon laskenta:**
  1. *Osumatarkistus:* `d20 + Hahmon bonus ≥ Kohteen AC`.
  2. *Vahingonlaskenta:* `Kyvyn perusvahinko + Tilapäiset/pysyvät bonukset (esim. +1 teroitettu ase)`. Kriittisellä osumalla vahinko kerrotaan kahdella.

---

### 3.3 Dialogi- ja Taitoheittojärjestelmä
* **Keskustelusolmut (`DialogueNodeSO.cs`):** Sisältävät NPC-tekstin ja interaktiiviset vaihtoehdot.
* **Taitoheitot dialogissa:** Tietty valinta voi vaatia d20-tarkistuksen (esim. `DC 13 Sotilaan kunnia`).
* **Sotilaallinen ja taktinen vaikuttaminen:** Dialogissa onnistuminen voi antaa suoraan etuja taisteluun (esim. laskea pomon AC:ta kahdeksi ensimmäiseksi vuoroksi).

---

## 4. HAHMOLUOKAT & KYVYT

### 4.1 Soturi: Sir Roland Rautakoura
* **Rooli:** Etulinjan panssaroitu suojelija ja vahingon vastaanottaja.
* **Ominaisuudet:** HP: Korkea | AC: Korkea | Vahinko: Keskitaso | Liikkuvuus: Matala
* **Lore:** Vartijakaartin veteraani, joka kaivoi vanhan rintapanssarinsa esiin puhdistaakseen entisen kotilinnansa epäkuolleista.
* **Kyvyt (4 kpl):**
  1. *Miekansivallus:* Perushyökkäys yhteen kohteeseen (Vahinko: `d20 + Voima`).
  2. *Kilpitorjunta:* Nostaa omaa puolustusta (`AC +3`) seuraavan viholliskierroksen ajaksi ja heikentää vihollisen osumatarkkuutta.
  3. *Sotahuuto:* Alueellinen puskenta, joka työntää vihollisia 1 ruudun taaksepäin ja nostaa tiimin osumatarkkuutta 2 vuoroksi.
  4. *Rautainen tahto:* Palauttaa välittömästi 30 % maksimikestopisteistä (kerran taistelussa).

---

### 4.2 Velho: Oppinut Elira/Elisa Tähtisilmä
* **Rooli:** Aluevahingon mestari ja taistelukentän hallitsija.
* **Ominaisuudet:** HP: Matala | AC: Matala | Vahinko: Erittäin korkea (AOE) | Liikkuvuus: Keskitaso
* **Lore:** Korkeakoulun tutkija, joka matkusti linnaan selvittämään sen kirotun taikuuden ja kadonneiden loitsukirjojen salaisuudet.
* **Kyvyt (4 kpl):**
  1. *Tulipallo (Fireball):* Räjähtävä aluehyökkäys 3x3 ruudukkoon (Vahinko: `d20 + Älykkyys` kaikille alueella oleville).
  2. *Jääriite (Frostbite):* Yhden kohteen vahinko, joka hidastaa vihollisen liikkumisnopeutta puolella 2 vuoron ajaksi.
  3. *Mana-kilpi:* Luo suojakentän, joka imee seuraavat 2 vihollishyökkäystä ennen rikkoutumistaan.
  4. *Teleportti (Blink):* Siirtää hahmon välittömästi mihin tahansa ruutuun 5 ruudun säteellä ilman vastahyökkäyksiä.

---

### 4.3 Varas: Varjo-Corvo
* **Rooli:** Yksittäisten kohteiden salamurhaaja, kriittiset osumat ja hyötytoiminnot.
* **Ominaisuudet:** HP: Keskitaso | AC: Keskitaso | Vahinko: Korkea (kriittinen pistevahinko) | Liikkuvuus: Erittäin korkea
* **Lore:** Opportunisti ja krouvien kasvatti, jolle linna on suuri ryöstettävä aarrekammio. Tuntee linnan salakäytävät paremmin kuin kukaan muu.
* **Kyvyt (4 kpl):**
  1. *Selkäänpuukotus (Backstab):* Suuri vahinko takaapäin tai suojattomaan kohteeseen (kaksinkertainen d20-bonus).
  2. *Savupommi:* Sokaisee lähialueen viholliset 1 vuoroksi ja antaa vapaan liikkumisvuoron.
  3. *Myrkkytikari:* Kevyt perusvahinko + myrkytystila (`d6` vahinkoa vuoron alussa 3 vuoron ajan).
  4. *Tiirikointi & Ansanpurku (Kenttäkyky):* Passiivinen/aktiivinen etu d20-heitoille aarrearkkujen ja salakäytävien avaamiseen.

---

## 5. PÄÄVASTUSTAJAT & POMOMEKANIIKAT

```
┌────────────────────────────────────────────────────────────────────────┐
│                          1. KIROTTU KOMENTAJA                          │
│  - Raskas kilpi (korkea AC)                                            │
│  - Kutsuu 2 luurankoa 50 % HP:ssa                                      │
│  - Dialogi: DC 13 Sotilaan kunnia (-2 AC pomolle 2 vuoroksi)           │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                         2. VARJOMAAGI MALAKOR                          │
│  - Teleportaatio & Peilikuvat                                          │
│  - Dialogi: DC 14 Arkaaninen herja (Paljastaa heti oikean pomon)       │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                       3. KIVETTYMISKUNINGAS (Pääpomo)                   │
│  - Vaihe 1: Maanjäristykset ja kivivyöryt                              │
│  - Vaihe 2: Muuttuu kiveksi (heijastaa loitsut)                        │
│  - Dialogi: DC 16 Pelottelu (-3 vuoroa pomon hyökkäysvoimaan)          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 6. TEKNINEN ARKKITEHTUURI & UNITY WEBGL OPTIMOINTI

### 6.1 Graphics & Rendering (URP)
* **Renderöintiputki:** Universal Render Pipeline (URP) Low/Medium-profiililla. Shader stripping päällä turhien koodirivien karsimiseksi.
* **Valaistus & Värimaailma:** Reaaliaikainen lämmin keltainen Directional Light + syvän sininen Ambient Light + sinertävä lineaarinen Fog.
* **Post-Processing Volume:** Vain yksi kevyt Volume: **Color Adjustments** (kontrasti & kylläisyys) ja matala **Bloom**.

### 6.2 Visuaalinen tyyli & Maasto
* **Tyylitelty Low-Poly-ilme:** Ei käytetä Unityn raskasta oletus-Terrainia. Maastot ja huoneet toteutetaan optimoituina Low-Poly Mesheinä (Blender / Polaris).
* **Texture Atlasing:** Tekstuurit yhdistetään suuriin Texture Atlas -kuviin (1024x1024 tai 2048x2048), mikä minimoi Draw Call -määrät verkkoselaimessa.

### 6.3 Koodiarkkitehtuuri & C# Skriptit
* **Tila-automaatti (State Machine):** `TurnManager.cs` ohjaa taistelun vaiheita.
* **ScriptableObject-pohja (SO):** `DialogueNodeSO.cs`, `AbilitySO.cs`, `EnemySO.cs` mahdollistavat modulaarisen datanhallinnan ilman raskaita ilmentämisiä.
* **Noppamekaniikka (`DiceSystem.cs`):** Singleton/Static-luokka, joka generoi arvon $1..20$, laskee bonukset ja laukaisee `OnDiceRolled`-tapahtuman visuaalista noppa-animaatiota ja käyttöliittymää varten.

### 6.4 Unity WebGL Build -asetukset
* **Compression Format:** *Brotli* tai *Gzip* (takaa pienen latauskoon verkkoselaimessa).
* **Code Stripping:** *High* (poistaa käyttämättömät Unity-moottorin osat WebAssembly-paketista).
* **Texture Compression:** *ASTC* tai *DXT/Crunch* selaintukea ja muistitehokkuutta varten.

---

## 7. PROJEKTIN MILESTONE-SUUNNITELMA

| Vaihe | Tavoite | Keskeiset toimenpiteet |
| :--- | :--- | :--- |
| **Vaihe 1: Prototyyppi** | Mekaniikan testaus | D20-noppaskripti (`DiceSystem.cs`), perusvuoropohjainen taistelu ruudukolla harmaalaatikoilla. |
| **Vaihe 2: Data & Dialogi** | Sisällön kytkentä | ScriptableObject-arkkitehtuuri, interaktiivinen dialogi-UI d20-taitoheitoilla. |
| **Vaihe 3: Kylä & Talous** | Keskusalueen logiikka | Sepän kauppavalikko, inventaariojärjestelmä, 3 sivutehtävän tila-seuranta. |
| **Vaihe 4: Visuaalit & Tasot** | Low-poly-viimeistely | Low-poly mesh-maastot, URP-valaistus, partikkeliefektit (loitsut, noppahehkut), pomomekaniikat. |
| **Vaihe 5: WebGL-optimointi** | Julkaisu valmis | Draw Call -optimointi, Brotli-pakkaus, testaus ja julkaisu itch.io-selainalustalla. |
