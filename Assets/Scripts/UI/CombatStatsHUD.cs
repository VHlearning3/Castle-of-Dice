using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Stat readouts that sit on the PlayerHUD:
    /// 1. Enemy_Stats_Strip (Top-Center, under Zone_Indicator_Banner): one card per living enemy in the
    ///    running battle with name, HP bar, AC, to-hit bonus, damage, reach, movement, special skill and
    ///    active status effects. Cards drop out when an enemy dies or is knocked out; the strip hides
    ///    outside combat.
    /// 2. Hero_Stats_Panel (lower half of the enlarged Hero_Status_Card): to-hit bonus, weapon bonus,
    ///    effective AC and movement, max HP / armor upgrades and the hero's active status effects.
    /// Each frame only integer snapshots are compared; text is rebuilt when a value actually changes,
    /// so the HUD stays allocation-free while nothing happens.
    /// </summary>
    public class CombatStatsHUD : MonoBehaviour
    {
        #region Layout Constants

        public const int MaxEnemyCards = 4;
        public const float EnemyCardWidth = 290f;
        public const float EnemyCardHeight = 104f;
        public const float EnemyCardGap = 12f;

        /// <summary>Top of the enemy strip, just below the zone banner (banner spans y -14 .. -64).</summary>
        public const float EnemyStripTop = -72f;

        /// <summary>Height of the Hero_Status_Card once the stats panel is added (was 138).</summary>
        public const float HeroCardHeight = 226f;

        private const float HeroStatsTop = -128f;
        private const float PlayerLookupInterval = 1f;

        private static readonly Color CreamText = new Color(1.0f, 0.96f, 0.88f, 1f);
        private static readonly Color GoldLabel = new Color(0.82f, 0.68f, 0.35f, 1f);
        private static readonly Color GoldValue = new Color(0.96f, 0.85f, 0.50f, 1f);
        private static readonly Color BuffGreen = new Color(0.45f, 0.87f, 0.50f, 1f);
        private static readonly Color DebuffBlue = new Color(0.55f, 0.80f, 1.0f, 1f);
        private static readonly Color RubyFill = new Color(0.85f, 0.18f, 0.15f, 1f);
        private static readonly Color CardFallbackColor = new Color(0.08f, 0.10f, 0.15f, 0.94f);
        private static readonly Color ActiveTurnTint = new Color(1f, 0.86f, 0.55f, 1f);

        /// <summary>Border of the highlighted enemy's card and the ring under its model.</summary>
        public static readonly Color FocusColor = new Color(0.30f, 0.90f, 1f, 1f);

        private const float FocusFrameThickness = 5f;
        private const float FocusFrameOutset = 4f;
        private const int FocusRingSegments = 40;

        /// <summary>Every displayable status effect, in the order they are listed.</summary>
        private static readonly StatusEffectType[] DisplayedEffects =
        {
            StatusEffectType.ShieldWall,
            StatusEffectType.ManaShield,
            StatusEffectType.AdvantageNextAttack,
            StatusEffectType.Poison,
            StatusEffectType.Frostbite,
            StatusEffectType.Blind
        };

        #endregion

        #region Nested Types

        private sealed class EnemyCard
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Background;
            public TMP_Text NameText;
            public TMP_Text ACText;
            public RectTransform BarFill;
            public TMP_Text VitalsText;
            public TMP_Text StatsText;
            public TMP_Text EffectsText;
            public GameObject FocusFrame;
            public EnemyUnit Unit;
            public long Signature;
        }

        #endregion

        #region Private State

        private Sprite panelSprite;
        private Sprite badgeSprite;
        private Sprite barTrackSprite;
        private Sprite barFillSprite;
        private Sprite dividerSprite;

        private RectTransform enemyStrip;
        private readonly List<EnemyCard> enemyCards = new List<EnemyCard>(MaxEnemyCards);
        private readonly List<EnemyUnit> visibleEnemies = new List<EnemyUnit>(MaxEnemyCards);
        private int shownEnemyCount = -1;

        private RectTransform heroPanel;
        private TMP_Text heroHitValue;
        private TMP_Text heroWeaponValue;
        private TMP_Text heroArmorValue;
        private TMP_Text heroMoveValue;
        private TMP_Text heroDetailText;
        private TMP_Text heroEffectsText;
        private PlayerUnit trackedPlayer;
        private long heroSignature;
        private bool heroDirty = true;
        private float nextPlayerLookup;

        private readonly StringBuilder sb = new StringBuilder(160);

        private EnemyUnit shownFocus;
        private LineRenderer focusRing;
        private Material focusRingMaterial;

        #endregion

        #region Public API

        /// <summary>The enemy card strip (null until <see cref="Build"/> ran).</summary>
        public RectTransform EnemyStrip => enemyStrip;

        /// <summary>The hero stats block inside the Hero_Status_Card (null until <see cref="Build"/> ran).</summary>
        public RectTransform HeroPanel => heroPanel;

        /// <summary>Number of enemy cards currently shown.</summary>
        public int ShownEnemyCount => shownEnemyCount < 0 ? 0 : shownEnemyCount;

        /// <summary>The enemy whose card is lit up right now (null when none).</summary>
        public EnemyUnit FocusedEnemy => shownFocus;

        /// <summary>The enemy shown on card <paramref name="index"/>, or null when that card is hidden.</summary>
        public EnemyUnit GetCardEnemy(int index)
        {
            if (index < 0 || index >= enemyCards.Count || index >= ShownEnemyCount) return null;
            return enemyCards[index].Unit;
        }

        /// <summary>Whether card <paramref name="index"/> currently shows its highlight border.</summary>
        public bool IsCardHighlighted(int index)
        {
            return index >= 0 && index < enemyCards.Count && enemyCards[index].FocusFrame != null
                && enemyCards[index].FocusFrame.activeSelf;
        }

        /// <summary>
        /// Builds (or re-links) both readouts. <paramref name="heroCard"/> is the Hero_Status_Card; the enemy
        /// strip is parented to this component's transform (the PlayerHUD root). Safe to call repeatedly.
        /// </summary>
        public void Build(RectTransform heroCard, Sprite panel, Sprite badge, Sprite barTrack, Sprite barFill, Sprite divider)
        {
            panelSprite = panel;
            badgeSprite = badge;
            barTrackSprite = barTrack;
            barFillSprite = barFill;
            dividerSprite = divider;

            BuildEnemyStrip();
            if (heroCard != null)
            {
                BuildHeroPanel(heroCard);
            }

            heroDirty = true;
            shownEnemyCount = -1;
        }

        /// <summary>Points the hero panel at a specific hero (PlayerHUD passes the one it tracks).</summary>
        public void SetPlayer(PlayerUnit player)
        {
            if (player == trackedPlayer) return;
            trackedPlayer = player;
            heroDirty = true;
        }

        /// <summary>Forces both readouts to rebuild on the next refresh.</summary>
        public void MarkDirty()
        {
            heroDirty = true;
            for (int i = 0; i < enemyCards.Count; i++)
            {
                enemyCards[i].Signature = 0;
            }
            shownEnemyCount = -1;
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (trackedPlayer == null && Time.unscaledTime >= nextPlayerLookup)
            {
                nextPlayerLookup = Time.unscaledTime + PlayerLookupInterval;
                SetPlayer(FindAnyObjectByType<PlayerUnit>(FindObjectsInactive.Include));
            }

            RefreshHero();

            TurnManager tm = TurnManager.Instance;
            bool combatActive = tm != null && tm.IsCombatActive;
            if (tm != null)
            {
                RefreshEnemies(tm.ActiveUnits, tm.CurrentActiveUnit, tm.IsCombatActive);
            }
            else
            {
                RefreshEnemies(null, null, false);
            }

            if (!combatActive && (EnemyFocus.Selected != null || EnemyFocus.CardHovered != null || EnemyFocus.TileHovered != null))
            {
                EnemyFocus.Clear();
            }
            RefreshFocus(combatActive ? EnemyFocus.Highlighted : null);
        }

        private void OnDestroy()
        {
            if (focusRing != null) Destroy(focusRing.gameObject);
            if (focusRingMaterial != null) Destroy(focusRingMaterial);
        }

        #endregion

        #region Enemy Focus

        /// <summary>
        /// Lights up the card of <paramref name="focus"/> with a bright border and puts a matching ring under
        /// its model, so it is clear which card belongs to which enemy. Null turns both off.
        /// </summary>
        public void RefreshFocus(EnemyUnit focus)
        {
            shownFocus = focus;

            int shown = ShownEnemyCount;
            for (int i = 0; i < enemyCards.Count; i++)
            {
                EnemyCard card = enemyCards[i];
                if (card.FocusFrame == null) continue;
                bool on = focus != null && i < shown && card.Unit == focus;
                if (card.FocusFrame.activeSelf != on) card.FocusFrame.SetActive(on);
            }

            UpdateFocusRing(focus);
        }

        private void UpdateFocusRing(EnemyUnit focus)
        {
            if (focus == null || !Application.isPlaying)
            {
                if (focusRing != null && focusRing.gameObject.activeSelf) focusRing.gameObject.SetActive(false);
                return;
            }

            if (focusRing == null) BuildFocusRing();
            if (!focusRing.gameObject.activeSelf) focusRing.gameObject.SetActive(true);

            Vector3 feet = focus.transform.position;
            GridManager grid = GridManager.Instance;
            if (grid != null) feet.y = grid.GetWorldPosition(focus.GridPosition).y;
            focusRing.transform.position = feet + Vector3.up * 0.07f;

            // Gentle pulse so the ring catches the eye without flickering
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            focusRing.transform.localScale = new Vector3(pulse, 1f, pulse);
        }

        private void BuildFocusRing()
        {
            GameObject ringObj = new GameObject("Enemy_Focus_Ring");
            focusRing = ringObj.AddComponent<LineRenderer>();
            focusRing.useWorldSpace = false;
            focusRing.loop = true;
            focusRing.positionCount = FocusRingSegments;
            focusRing.widthMultiplier = 0.09f;
            focusRing.numCornerVertices = 2;
            focusRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            focusRing.receiveShadows = false;

            // Sprites/Default is in Always Included Shaders, so it survives WebGL shader stripping
            // (the ring lives in the battle scene and is rebuilt after a scene change; the material is kept)
            if (focusRingMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null) focusRingMaterial = new Material(shader) { name = "Enemy_Focus_Ring" };
            }
            if (focusRingMaterial != null) focusRing.sharedMaterial = focusRingMaterial;
            focusRing.startColor = FocusColor;
            focusRing.endColor = FocusColor;

            float tile = GridManager.Instance != null ? GridManager.Instance.EffectiveTileSize : 1.6f;
            float radius = tile * 0.42f;
            for (int i = 0; i < FocusRingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / FocusRingSegments;
                focusRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        #endregion

        #region Enemy Strip

        /// <summary>
        /// Syncs the enemy cards with the given battle roster. Only living <see cref="EnemyUnit"/>s are shown;
        /// the strip hides when <paramref name="combatActive"/> is false.
        /// </summary>
        public void RefreshEnemies(IReadOnlyList<CombatUnit> units, CombatUnit activeUnit, bool combatActive)
        {
            if (enemyStrip == null) return;

            visibleEnemies.Clear();
            if (combatActive && units != null)
            {
                for (int i = 0; i < units.Count && visibleEnemies.Count < MaxEnemyCards; i++)
                {
                    if (units[i] is EnemyUnit enemy && enemy != null && enemy.IsAlive)
                    {
                        visibleEnemies.Add(enemy);
                    }
                }
            }

            int count = visibleEnemies.Count;
            if (count != shownEnemyCount)
            {
                LayoutEnemyCards(count);
            }

            for (int i = 0; i < count; i++)
            {
                EnemyCard card = enemyCards[i];
                EnemyUnit enemy = visibleEnemies[i];
                bool isActive = enemy == activeUnit;
                long signature = ComputeEnemySignature(enemy, isActive);
                if (card.Unit == enemy && card.Signature == signature) continue;

                card.Unit = enemy;
                card.Signature = signature;
                WriteEnemyCard(card, enemy, isActive);
            }
        }

        private void LayoutEnemyCards(int count)
        {
            shownEnemyCount = count;
            enemyStrip.gameObject.SetActive(count > 0);

            while (enemyCards.Count < count)
            {
                enemyCards.Add(CreateEnemyCard(enemyCards.Count));
            }

            float total = count * EnemyCardWidth + Mathf.Max(0, count - 1) * EnemyCardGap;
            float left = (enemyStrip.rect.width > 0f ? enemyStrip.rect.width : total) * 0.5f - total * 0.5f;
            for (int i = 0; i < enemyCards.Count; i++)
            {
                EnemyCard card = enemyCards[i];
                bool show = i < count;
                card.Root.SetActive(show);
                if (!show)
                {
                    card.Unit = null;
                    card.Signature = 0;
                    continue;
                }
                card.Rect.anchoredPosition = new Vector2(left + i * (EnemyCardWidth + EnemyCardGap), 0f);
            }
        }

        private void WriteEnemyCard(EnemyCard card, EnemyUnit enemy, bool isActiveTurn)
        {
            Color baseColor = panelSprite != null ? Color.white : CardFallbackColor;
            card.Background.color = isActiveTurn ? baseColor * ActiveTurnTint : baseColor;
            card.NameText.text = string.IsNullOrEmpty(enemy.UnitName) ? "Enemy" : enemy.UnitName;

            sb.Clear();
            sb.Append("AC ").Append(enemy.ArmorClass);
            card.ACText.text = sb.ToString();

            int max = Mathf.Max(1, enemy.MaxHP);
            int hp = Mathf.Clamp(enemy.CurrentHP, 0, max);
            card.BarFill.anchorMax = new Vector2((float)hp / max, 1f);

            sb.Clear();
            sb.Append("HP ").Append(hp).Append(" / ").Append(max);
            card.VitalsText.text = sb.ToString();

            sb.Clear();
            sb.Append("<color=#D1AD59>To hit</color> ");
            AppendSigned(sb, enemy.AttackBonus);
            sb.Append("   <color=#D1AD59>Dmg</color> ").Append(enemy.AttackDamage);
            sb.Append("   <color=#D1AD59>Reach</color> ").Append(enemy.AttackRange);
            sb.Append("   <color=#D1AD59>Move</color> ").Append(enemy.MovementRange);
            card.StatsText.text = sb.ToString();

            sb.Clear();
            bool any = false;
            if (enemy.SpecialAbility != null && !string.IsNullOrEmpty(enemy.SpecialAbility.AbilityName))
            {
                sb.Append("<color=#D1AD59>Skill:</color> ").Append(enemy.SpecialAbility.AbilityName);
                any = true;
            }
            if (enemy.StatusEffects != null && HasAnyEffect(enemy.StatusEffects))
            {
                if (any) sb.Append("  •  ");
                AppendEffects(sb, enemy.StatusEffects);
                any = true;
            }
            if (!any)
            {
                sb.Append("<color=#8A93A3>No effects</color>");
            }
            card.EffectsText.text = sb.ToString();
        }

        private static long ComputeEnemySignature(EnemyUnit enemy, bool isActive)
        {
            unchecked
            {
                long h = 17;
                h = h * 31 + enemy.CurrentHP;
                h = h * 31 + enemy.MaxHP;
                h = h * 31 + enemy.ArmorClass;
                h = h * 31 + enemy.AttackBonus;
                h = h * 31 + enemy.AttackDamage;
                h = h * 31 + enemy.AttackRange;
                h = h * 31 + enemy.MovementRange;
                h = h * 31 + (isActive ? 1 : 2);
                h = h * 31 + (enemy.UnitName != null ? enemy.UnitName.GetHashCode() : 0);
                h = h * 31 + (enemy.SpecialAbility != null ? enemy.SpecialAbility.GetInstanceID() : 0);
                h = h * 31 + ComputeEffectSignature(enemy.StatusEffects);
                return h == 0 ? 1 : h;
            }
        }

        #endregion

        #region Hero Panel

        /// <summary>Rewrites the hero stats block when any displayed value changed (or when forced).</summary>
        public void RefreshHero(bool force = false)
        {
            if (heroPanel == null) return;

            PlayerUnit player = trackedPlayer;
            if (player == null)
            {
                if (heroDirty || force)
                {
                    heroDirty = false;
                    heroSignature = 0;
                    heroHitValue.text = "--";
                    heroWeaponValue.text = "--";
                    heroArmorValue.text = "--";
                    heroMoveValue.text = "--";
                    heroDetailText.text = "";
                    heroEffectsText.text = "";
                }
                return;
            }

            long signature = ComputeHeroSignature(player);
            if (!force && !heroDirty && signature == heroSignature) return;
            heroDirty = false;
            heroSignature = signature;

            StatusEffectController effects = player.StatusEffects;

            sb.Clear();
            AppendSigned(sb, player.PrimaryAttributeBonus);
            heroHitValue.text = sb.ToString();

            sb.Clear();
            AppendSigned(sb, player.WeaponDamageBonus);
            heroWeaponValue.text = sb.ToString();

            sb.Clear();
            sb.Append(player.ArmorClass);
            heroArmorValue.text = sb.ToString();
            heroArmorValue.color = effects != null && effects.GetArmorClassBonus() > 0 ? BuffGreen : CreamText;

            sb.Clear();
            sb.Append(player.MovementRange);
            heroMoveValue.text = sb.ToString();
            heroMoveValue.color = effects != null && effects.HasEffect(StatusEffectType.Frostbite) ? DebuffBlue : CreamText;

            sb.Clear();
            sb.Append("<color=#D1AD59>Max HP</color> ").Append(player.MaxHP);
            if (player.MaxHPBonus > 0)
            {
                sb.Append(" (+").Append(player.MaxHPBonus).Append(')');
            }
            int effectAC = effects != null ? effects.GetArmorClassBonus() : 0;
            sb.Append("   <color=#D1AD59>Base AC</color> ").Append(player.ArmorClass - player.ArmorClassBonus - effectAC);
            sb.Append("   <color=#D1AD59>Armor upgrades</color> ");
            AppendSigned(sb, player.ArmorClassBonus);
            heroDetailText.text = sb.ToString();

            sb.Clear();
            sb.Append("<color=#D1AD59>Effects:</color> ");
            if (effects != null && HasAnyEffect(effects))
            {
                AppendEffects(sb, effects);
            }
            else
            {
                sb.Append("<color=#8A93A3>none</color>");
            }
            heroEffectsText.text = sb.ToString();
        }

        private static long ComputeHeroSignature(PlayerUnit player)
        {
            unchecked
            {
                long h = 23;
                h = h * 31 + player.PrimaryAttributeBonus;
                h = h * 31 + player.WeaponDamageBonus;
                h = h * 31 + player.ArmorClass;
                h = h * 31 + player.ArmorClassBonus;
                h = h * 31 + player.MovementRange;
                h = h * 31 + player.MaxHP;
                h = h * 31 + player.MaxHPBonus;
                h = h * 31 + ComputeEffectSignature(player.StatusEffects);
                return h == 0 ? 1 : h;
            }
        }

        #endregion

        #region Status Effect Formatting

        /// <summary>Short player-facing name for a status effect.</summary>
        public static string EffectLabel(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison: return "Poisoned";
                case StatusEffectType.Frostbite: return "Frostbite";
                case StatusEffectType.Blind: return "Blinded";
                case StatusEffectType.ManaShield: return "Mana Shield";
                case StatusEffectType.AdvantageNextAttack: return "Advantage";
                case StatusEffectType.ShieldWall: return "Shield Wall";
                default: return type.ToString();
            }
        }

        /// <summary>Buffs render green, debuffs in their element colour.</summary>
        public static string EffectColorHex(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison: return "#8BE36B";
                case StatusEffectType.Frostbite: return "#8CCBFF";
                case StatusEffectType.Blind: return "#B8A6D9";
                default: return "#F6D57A";
            }
        }

        private static bool HasAnyEffect(StatusEffectController effects)
        {
            for (int i = 0; i < DisplayedEffects.Length; i++)
            {
                if (effects.HasEffect(DisplayedEffects[i])) return true;
            }
            return false;
        }

        /// <summary>Appends "Poisoned 2 • Shield Wall 1" (remaining turns) for every active effect.</summary>
        public static void AppendEffects(StringBuilder builder, StatusEffectController effects)
        {
            if (effects == null) return;

            bool first = true;
            for (int i = 0; i < DisplayedEffects.Length; i++)
            {
                StatusEffectType type = DisplayedEffects[i];
                if (!effects.HasEffect(type)) continue;

                if (!first) builder.Append("  •  ");
                first = false;
                builder.Append("<color=").Append(EffectColorHex(type)).Append('>')
                       .Append(EffectLabel(type)).Append(' ').Append(effects.GetRemainingTurns(type))
                       .Append("</color>");
            }
        }

        private static long ComputeEffectSignature(StatusEffectController effects)
        {
            if (effects == null) return 0;
            unchecked
            {
                long h = 0;
                for (int i = 0; i < DisplayedEffects.Length; i++)
                {
                    h = h * 31 + (effects.HasEffect(DisplayedEffects[i]) ? effects.GetRemainingTurns(DisplayedEffects[i]) + 1 : 0);
                }
                return h;
            }
        }

        private static void AppendSigned(StringBuilder builder, int value)
        {
            if (value >= 0) builder.Append('+');
            builder.Append(value);
        }

        #endregion

        #region Hierarchy Construction

        private void BuildEnemyStrip()
        {
            Transform existing = transform.Find("Enemy_Stats_Strip");
            GameObject stripObj = existing != null ? existing.gameObject : new GameObject("Enemy_Stats_Strip", typeof(RectTransform));
            stripObj.transform.SetParent(transform, false);
            enemyStrip = stripObj.GetComponent<RectTransform>();
            enemyStrip.anchorMin = new Vector2(0.5f, 1f);
            enemyStrip.anchorMax = new Vector2(0.5f, 1f);
            enemyStrip.pivot = new Vector2(0.5f, 1f);
            enemyStrip.anchoredPosition = new Vector2(0f, EnemyStripTop);
            enemyStrip.sizeDelta = new Vector2(MaxEnemyCards * EnemyCardWidth + (MaxEnemyCards - 1) * EnemyCardGap, EnemyCardHeight);

            // Re-link cards saved into the scene by an editor build so they aren't duplicated
            enemyCards.Clear();
            for (int i = 0; i < MaxEnemyCards; i++)
            {
                Transform cardTr = enemyStrip.Find("Foe_Card_" + i);
                if (cardTr == null) break;
                enemyCards.Add(CreateEnemyCard(i));
            }

            stripObj.SetActive(false);
        }

        private EnemyCard CreateEnemyCard(int index)
        {
            EnemyCard card = new EnemyCard();

            string cardName = "Foe_Card_" + index;
            Transform cardTr = enemyStrip.Find(cardName);
            GameObject cardObj = cardTr != null ? cardTr.gameObject : new GameObject(cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardObj.transform.SetParent(enemyStrip, false);
            card.Root = cardObj;
            card.Rect = cardObj.GetComponent<RectTransform>();
            SetTopLeft(card.Rect, Vector2.zero, new Vector2(EnemyCardWidth, EnemyCardHeight));

            card.Background = cardObj.GetComponent<Image>();
            ApplySprite(card.Background, panelSprite, CardFallbackColor);

            // Name (top-left)
            card.NameText = EnsureText(cardObj.transform, "Foe_Name_Text", 14f, FontStyles.Bold, CreamText, TextAlignmentOptions.MidlineLeft);
            SetTopLeft(card.NameText.rectTransform, new Vector2(14f, -10f), new Vector2(190f, 20f));
            card.NameText.overflowMode = TextOverflowModes.Ellipsis;

            // AC badge (top-right)
            GameObject badgeObj = EnsureChild(cardObj.transform, "Foe_AC_Badge", typeof(CanvasRenderer), typeof(Image));
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-12f, -9f);
            badgeRect.sizeDelta = new Vector2(64f, 22f);
            ApplySprite(badgeObj.GetComponent<Image>(), badgeSprite, new Color(0.2f, 0.16f, 0.1f, 1f));
            card.ACText = EnsureText(badgeObj.transform, "Foe_AC_Text", 11.5f, FontStyles.Bold, GoldValue, TextAlignmentOptions.Center);
            Stretch(card.ACText.rectTransform);

            // Vitals bar
            GameObject barObj = EnsureChild(cardObj.transform, "Foe_Vitals_Bar", typeof(CanvasRenderer), typeof(Image));
            SetTopLeft(barObj.GetComponent<RectTransform>(), new Vector2(14f, -34f), new Vector2(EnemyCardWidth - 28f, 20f));
            ApplySprite(barObj.GetComponent<Image>(), barTrackSprite, new Color(0.1f, 0.05f, 0.05f, 1f));

            GameObject fillObj = EnsureChild(barObj.transform, "Foe_Vitals_Fill", typeof(CanvasRenderer), typeof(Image));
            card.BarFill = fillObj.GetComponent<RectTransform>();
            card.BarFill.anchorMin = Vector2.zero;
            card.BarFill.anchorMax = Vector2.one;
            card.BarFill.pivot = new Vector2(0f, 0.5f);
            card.BarFill.offsetMin = new Vector2(2f, 2f);
            card.BarFill.offsetMax = new Vector2(-2f, -2f);
            Image fillImg = fillObj.GetComponent<Image>();
            ApplySprite(fillImg, barFillSprite, RubyFill);
            fillImg.color = RubyFill;

            card.VitalsText = EnsureText(barObj.transform, "Foe_Vitals_Text", 12f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Stretch(card.VitalsText.rectTransform);

            // Attack profile
            card.StatsText = EnsureText(cardObj.transform, "Foe_Stats_Text", 11.5f, FontStyles.Normal, UITheme.SoftText, TextAlignmentOptions.MidlineLeft);
            SetTopLeft(card.StatsText.rectTransform, new Vector2(14f, -58f), new Vector2(EnemyCardWidth - 28f, 18f));
            card.StatsText.richText = true;

            // Skill & status effects
            card.EffectsText = EnsureText(cardObj.transform, "Foe_Effects_Text", 11f, FontStyles.Normal, UITheme.SoftText, TextAlignmentOptions.MidlineLeft);
            SetTopLeft(card.EffectsText.rectTransform, new Vector2(14f, -78f), new Vector2(EnemyCardWidth - 28f, 18f));
            card.EffectsText.richText = true;
            card.EffectsText.overflowMode = TextOverflowModes.Ellipsis;

            // Highlight border: four bright edges drawn over the card while its enemy is pointed at
            GameObject frameObj = EnsureChild(cardObj.transform, "Foe_Focus_Frame");
            RectTransform frameRect = frameObj.GetComponent<RectTransform>();
            Stretch(frameRect);
            frameRect.offsetMin = new Vector2(-FocusFrameOutset, -FocusFrameOutset); // hugs the card from outside
            frameRect.offsetMax = new Vector2(FocusFrameOutset, FocusFrameOutset);
            BuildFrameEdge(frameObj.transform, "Edge_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FocusFrameThickness));
            BuildFrameEdge(frameObj.transform, "Edge_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FocusFrameThickness));
            BuildFrameEdge(frameObj.transform, "Edge_Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(FocusFrameThickness, 0f));
            BuildFrameEdge(frameObj.transform, "Edge_Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(FocusFrameThickness, 0f));
            frameObj.transform.SetAsLastSibling();
            frameObj.SetActive(false);
            card.FocusFrame = frameObj;

            // Hovering or clicking the card lights up its enemy's model too
            EnemyCardPointer pointer = cardObj.GetComponent<EnemyCardPointer>();
            if (pointer == null) pointer = cardObj.AddComponent<EnemyCardPointer>();
            pointer.Owner = this;
            pointer.Index = index;
            card.Background.raycastTarget = true;

            return card;
        }

        private static void BuildFrameEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            GameObject edge = EnsureChild(parent, name, typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image img = edge.GetComponent<Image>();
            img.color = FocusColor;
            img.raycastTarget = false;
        }

        private void BuildHeroPanel(RectTransform heroCard)
        {
            GameObject panelObj = EnsureChild(heroCard, "Hero_Stats_Panel");
            heroPanel = panelObj.GetComponent<RectTransform>();
            heroPanel.anchorMin = new Vector2(0f, 1f);
            heroPanel.anchorMax = new Vector2(1f, 1f);
            heroPanel.pivot = new Vector2(0.5f, 1f);
            heroPanel.anchoredPosition = new Vector2(0f, HeroStatsTop);
            heroPanel.sizeDelta = new Vector2(-32f, HeroCardHeight + HeroStatsTop);

            // Gold divider separating identity/vitals from the stat block
            GameObject dividerObj = EnsureChild(panelObj.transform, "Hero_Stats_Divider", typeof(CanvasRenderer), typeof(Image));
            RectTransform dRect = dividerObj.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0f, 1f);
            dRect.anchorMax = new Vector2(1f, 1f);
            dRect.pivot = new Vector2(0.5f, 1f);
            dRect.anchoredPosition = Vector2.zero;
            dRect.sizeDelta = new Vector2(0f, 4f);
            Image divImg = dividerObj.GetComponent<Image>();
            ApplySprite(divImg, dividerSprite, new Color(0.82f, 0.68f, 0.35f, 0.6f));
            divImg.raycastTarget = false;

            const float cellWidth = 104f;
            const float cellGap = 4f;
            heroHitValue = BuildStatCell(panelObj.transform, "Stat_Cell_Hit", "TO HIT", 0, cellWidth, cellGap);
            heroWeaponValue = BuildStatCell(panelObj.transform, "Stat_Cell_Weapon", "WEAPON", 1, cellWidth, cellGap);
            heroArmorValue = BuildStatCell(panelObj.transform, "Stat_Cell_Armor", "ARMOR", 2, cellWidth, cellGap);
            heroMoveValue = BuildStatCell(panelObj.transform, "Stat_Cell_Move", "MOVE", 3, cellWidth, cellGap);

            heroDetailText = EnsureText(panelObj.transform, "Hero_Stats_Detail_Text", 11.5f, FontStyles.Normal, UITheme.SoftText, TextAlignmentOptions.MidlineLeft);
            SetTopLeftStretch(heroDetailText.rectTransform, -52f, 18f);
            heroDetailText.richText = true;

            heroEffectsText = EnsureText(panelObj.transform, "Hero_Effects_Text", 11.5f, FontStyles.Normal, UITheme.SoftText, TextAlignmentOptions.MidlineLeft);
            SetTopLeftStretch(heroEffectsText.rectTransform, -72f, 18f);
            heroEffectsText.richText = true;
            heroEffectsText.overflowMode = TextOverflowModes.Ellipsis;
        }

        private TMP_Text BuildStatCell(Transform parent, string name, string label, int index, float width, float gap)
        {
            GameObject cellObj = EnsureChild(parent, name, typeof(CanvasRenderer), typeof(Image));
            RectTransform cellRect = cellObj.GetComponent<RectTransform>();
            SetTopLeft(cellRect, new Vector2(index * (width + gap), -8f), new Vector2(width, 40f));
            Image cellBg = cellObj.GetComponent<Image>();
            ApplySprite(cellBg, badgeSprite, new Color(0.2f, 0.16f, 0.1f, 0.9f));
            cellBg.raycastTarget = false;

            TMP_Text labelText = EnsureText(cellObj.transform, "Label", 9.5f, FontStyles.Bold, GoldLabel, TextAlignmentOptions.Center);
            RectTransform lRect = labelText.rectTransform;
            lRect.anchorMin = new Vector2(0f, 0.55f);
            lRect.anchorMax = new Vector2(1f, 1f);
            lRect.offsetMin = new Vector2(0f, 0f);
            lRect.offsetMax = new Vector2(0f, -3f);
            labelText.characterSpacing = 1.5f;
            labelText.text = label;

            TMP_Text valueText = EnsureText(cellObj.transform, "Value", 16f, FontStyles.Bold, CreamText, TextAlignmentOptions.Center);
            RectTransform vRect = valueText.rectTransform;
            vRect.anchorMin = new Vector2(0f, 0f);
            vRect.anchorMax = new Vector2(1f, 0.6f);
            vRect.offsetMin = new Vector2(0f, 2f);
            vRect.offsetMax = Vector2.zero;
            valueText.text = "--";
            return valueText;
        }

        #endregion

        #region UI Helpers

        private static GameObject EnsureChild(Transform parent, string name, params System.Type[] components)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            GameObject go = new GameObject(name, typeof(RectTransform));
            for (int i = 0; i < components.Length; i++)
            {
                go.AddComponent(components[i]);
            }
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text EnsureText(Transform parent, string name, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = EnsureChild(parent, name, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void ApplySprite(Image image, Sprite sprite, Color fallback)
        {
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = fallback;
            }
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopLeftStretch(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(2f, top);
            rect.sizeDelta = new Vector2(-4f, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
