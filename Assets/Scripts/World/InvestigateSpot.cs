using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Something worth a closer look (Vili, 2026-10-06): a skill check (Perception, Investigation, Arcana,
    /// History, Insight) against a DC. A success reveals a scrap of lore (and sometimes a few coins) and is
    /// remembered in the save; a failure leaves it for the next visit. Built in code by
    /// <see cref="InvestigationSpawner"/>.
    /// </summary>
    public class InvestigateSpot : Interactable
    {
        [SerializeField] private string spotId;

        private InvestigationSpawner.Spot spot;
        private bool triedThisVisit;
        private bool resolving;

        /// <summary>Story flag set when the spot has given up its secret.</summary>
        public static string FoundFlag(string id) => "Investigated:" + id;

        /// <summary>This spot's id.</summary>
        public string SpotId => spotId;

        /// <summary>Whether the hero already found what is here.</summary>
        public bool IsSolved => StoryFlags.Has(FoundFlag(spotId));

        private void Start()
        {
            PickupBeacon.Attach(transform, PickupBeacon.NoteColor, () => !IsSolved && !triedThisVisit, 0.6f);
        }

        public override void Interact(PlayerUnit player)
        {
            if (resolving || player == null) return;

            if (IsSolved)
            {
                StoryPanelUI.Show(spot.Title, spot.Body);
                return;
            }
            if (triedThisVisit)
            {
                LockpickMinigameUI.ShowToast(spot.FailText + " (Come back later.)");
                return;
            }

            triedThisVisit = true;
            resolving = true;
            int bonus = HeroAttributes.GetSkillBonus(player, spot.Skill);
            string title = $"{HeroAttributes.GetSkillName(spot.Skill)} ({HeroAttributes.GetShortName(HeroAttributes.GetSkillAttribute(spot.Skill))})";
            RerollableRoll.Roll(bonus, spot.DC, AdvantageType.None, result =>
            {
                resolving = false;
                if (!result.isSuccess)
                {
                    LockpickMinigameUI.ShowToast(spot.FailText);
                    return;
                }

                StoryFlags.Set(FoundFlag(spotId));
                if (spot.Gold > 0 && InventoryManager.Instance != null) InventoryManager.Instance.AddGold(spot.Gold);
                string reward = spot.Gold > 0 ? $"\n\n<color=#FFD34D>+{spot.Gold} gold</color>" : string.Empty;
                StoryPanelUI.Show(spot.Title, spot.Body + reward);
            }, title);
        }

        /// <summary>Builds a spot: a small cairn of stones with a soft glow, at <paramref name="position"/>.</summary>
        public static InvestigateSpot Create(InvestigationSpawner.Spot definition, Vector3 position)
        {
            GameObject root = new GameObject("Investigate_" + definition.Id);
            root.transform.position = position;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material stone = shader != null ? new Material(shader) { name = "Investigate_Stone" } : null;
            if (stone != null)
            {
                Color grey = new Color(0.46f, 0.44f, 0.42f);
                if (stone.HasProperty("_BaseColor")) stone.SetColor("_BaseColor", grey);
                stone.color = grey;
            }

            Rock(root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.6f, 0.24f, 0.5f), 10f, stone);
            Rock(root.transform, new Vector3(0.05f, 0.34f, 0.02f), new Vector3(0.42f, 0.2f, 0.36f), 35f, stone);
            Rock(root.transform, new Vector3(-0.02f, 0.5f, 0f), new Vector3(0.26f, 0.16f, 0.22f), 70f, stone);

            BoxCollider click = root.AddComponent<BoxCollider>();
            click.center = new Vector3(0f, 0.4f, 0f);
            click.size = new Vector3(1.1f, 0.8f, 1.1f);

            InvestigateSpot spot = root.AddComponent<InvestigateSpot>();
            spot.spotId = definition.Id;
            spot.spot = definition;
            spot.promptMessage = $"{definition.Prompt} ({HeroAttributes.GetSkillName(definition.Skill)} DC {definition.DC})";
            spot.interactionRadius = 3f;
            return spot;
        }

        private static void Rock(Transform parent, Vector3 localPos, Vector3 scale, float yaw, Material mat)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Cairn_Stone";
            Collider col = rock.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            rock.transform.SetParent(parent, false);
            rock.transform.localPosition = localPos;
            rock.transform.localRotation = Quaternion.Euler(0f, yaw, 4f);
            rock.transform.localScale = scale;
            Renderer r = rock.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }
    }
}
