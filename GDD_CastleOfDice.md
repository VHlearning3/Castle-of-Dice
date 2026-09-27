# 📜 Game Design Document (GDD): Castle of Dice

**Versio:** 2.0  
**Moottori:** Unity 6 (6000.3.14f1 LTS) WebGL  
**Tyylilaji:** Taktinen D20-Pöytäroolipeli / Dungeon Crawler  

---

## 1. Pelisuunnittelun Pääpilarit

1. **Aito D20-tuntuma ilman kitkaa:** Pelissä käytetään D&D 5e -henkistä kaavaa `d20 + modifier ≥ DC / AC`. Arjen rutiiniheitot automatisoidaan nopsaan tahtiin, mutta kohtalokkaat heitot pysäytetään heittoruutuun taktista miettimistaukoa varten.
2. **Pelaajan Taktinen Vapaus (Player Agency):** Esteet voi voittaa suoralla taistelulla (Soturi), maagisella aluevahingolla (Velho) tai tiirikoinnilla, hiipimisellä ja dialogitestauksella (Varas).
3. **Palkitseva Seikkailusilmukka (Core Loop):**
   ```text
   Oakhavenin Kylä (Paja & Tehtävät) ──► Linnan Siipien Tutkiminen ──► D20-Taistelu & Salaisuudet ──► Pomon Voitto & Milestone Level-Up
   ```

---

## 2. Sankariluokat & Kyvyt

### 🛡️ Soturi (Sir Roland Rautakoura)
- **Tilastot:** 30 HP, 14 AC, Liike 3 ruutua. Pääattribuutti: Voima (+3).
- **Kyvyt:**
  1. *Miekansivallus:* Perushyökkäys 1d8 + 3 vahinkoa.
  2. *Kilpitorjunta:* Nostaa puolustusta +3 AC vuoron ajaksi.
  3. *Sotahuuto:* Työntää vihollisia 1 ruudun taaksepäin.
  4. *Rautainen tahto:* Palauttaa 30 % max HP:sta kerran taistelussa.

### 🔮 Velho (Oppinut Elira)
- **Tilastot:** 20 HP, 12 AC, Liike 3 ruutua. Pääattribuutti: Älykkyys (+3).
- **Kyvyt:**
  1. *Arkaaninen nuoli:* Kantama 4 ruutua, 1d6 + 3 taikavahinkoa.
  2. *Tulipallo:* 3x3 aluevahinko, 2d6 vahinkoa kaikille ruuduissa oleville.
  3. *Mana-kilpi:* Imee seuraavat 10 vahinkopistettä.
  4. *Teleportti (Blink):* Siirtyy välittömästi 5 ruutua ilman vastahyökkäyksiä.

### 🗡️ Varas (Varjo-Corvo)
- **Tilastot:** 25 HP, 13 AC, Liike 4 ruutua. Pääattribuutti: Ketteryys (+3).
- **Kyvyt:**
  1. *Myrkkytikari:* 1d4 vahinkoa + 1d6 myrkkyä/vuoro 3 vuoron ajan.
  2. *Selkäänpuukotus:* Advantage-heitto (2d20), kriittisestä osumasta 3x vahinko.
  3. *Savupommi:* Sokeuttaa 2x2 alueen viholliset 1 vuoroksi.
  4. *Tiirikointi & Ansanpurku:* Avaa salareittejä ja kätkettyjä aarrearkkuja.

---

## 3. Milestone-Tasonnousujärjestelmä

Tasonnousu ei perustu XP-grindaukseen, vaan linnan siipien läpäisyyn:

- **Level 1 (Aloitus):** Kylä, kellari ja metsäpolku.
- **Level 2 (Linnan Veteraani):** Voittamalla Kirottu Komentaja tai tirikoimalla salareitti.
- **Level 3 (Max Level - Arkaaninen Murskaaja):** Voittamalla Varjomaagi Malakor.

### Tasonnousun Valinnat:
1. **Sankarin Sitkeys:** +5 Max HP & Täysparannus.
2. **Ominaisuusbonuksen Kasvu:** +1 Pääattribuuttiin (nostaa osumatarkkuutta & vahinkoa).
3. **Kyvyn Mahdistaminen:** Päivittää yhden luokkakyvyn Rank 2 -tasolle.

---

## 4. Talous & Palkintomekaniikat

- **Romumetalli (Scrap Metal):** Taistelun voittamisesta saa aina **2–10 kpl romumetallia**.
- **Seppä Baldurin Paja:**
  - 1 Romumetalli = 10 Kultaa.
  - Terveysjuoma = 25 Kultaa (+15 HP).
  - Teroitettu miekka = 60 Kultaa (+1 Pysyvä vahinko).
  - Riimukilpi / Haarniska = 100 Kultaa (+1 Pysyvä AC).
- **NPC-Tehtäväpalkinnot:**
  - Jokaiseen suoritettuun tehtävään liittyy `50g + 2 HP potion` -palkinto.
  - Tehtäväesineet (*Sormus*, *Suokukat*) näkyvät `X/X`-muodossa eikä niitä voi pudottaa tai tuhota.

---

## 5. Päävastustajat (Boss Mechanics)

1. **Kirottu Komentaja (Alapiha):** 50 HP, 16 AC. Kutsuu 2 luurankoa 50 % HP:ssa. *DC 13 Dialogi* laskee AC:ta -2 pykälää.
2. **Varjomaagi Malakor (Kirjasto):** 40 HP, 13 AC. Teleporttaa vahingosta ja luo peilikuvaklooneja. *DC 14 Dialogi* paljastaa aito-Malakorin.
3. **Kivettymiskuningas (Kruununsali):** 60 HP, 15 AC (Vaihe 1) -> 18 AC (Vaihe 2 Stone Form). Maanjäristykset ja taikapeilaus. *DC 16 Dialogi* heikentää hyökkäysvoimaa -3 yksikköä.