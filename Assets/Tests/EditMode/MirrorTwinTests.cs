using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Editor;
using CastleOfTheD20.Economy;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The Tower's secret boss: the Mirror Twin's model, takes and animator, her fighting profile, the encounter
    /// waiting in the mirror room, the hidden quest and the tavern elf's warning.
    /// </summary>
    [TestFixture]
    public class MirrorTwinTests
    {
        [TearDown]
        public void LeaveEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Model_IsHumanoidWithAllTakes()
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(MirrorTwinSetup.ModelPath).OfType<Avatar>().FirstOrDefault();
            Assert.IsNotNull(avatar, "the model builds an avatar");
            Assert.IsTrue(avatar.isValid && avatar.isHuman, "a valid humanoid avatar");

            var clips = AssetDatabase.LoadAllAssetsAtPath(MirrorTwinSetup.AnimsPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            foreach (string take in new[] { "Idle", "Walk", "Attack", "Cast", "Laugh", "Hit", "Die" })
            {
                Assert.IsTrue(clips.ContainsKey(take), "take " + take);
            }
            Assert.IsTrue(clips["Idle"].isLooping && clips["Walk"].isLooping, "idle and walk loop");
            Assert.IsFalse(clips["Attack"].isLooping, "the thrust plays once");

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorTwinSetup.ModelPath);
            var names = model.GetComponentsInChildren<Renderer>(true).Select(r => r.name).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "Twin_Body", "Twin_Skirt", "Twin_Belt", "Twin_Trident" }, names);
            Assert.IsNotNull(model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Trident_Head"), "the trident's head mark");
            Assert.AreEqual(10, model.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Skirt_")), "five chains of skirt bones");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorTwinSetup.PrefabPath);
            Assert.IsNotNull(prefab.GetComponent<SkirtSway>(), "the skirt sways");
            Renderer skirt = prefab.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Twin_Skirt");
            Assert.IsNotNull(skirt.sharedMaterial.mainTexture, "the skirt wears its cloth texture");
        }

        [Test]
        public void Animator_HasTheBossStatesAndTheGrin()
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(MirrorTwinSetup.ControllerPath);
            Assert.IsNotNull(ac);
            var states = ac.layers[0].stateMachine.states.Select(s => s.state).ToDictionary(s => s.name);
            foreach (string s in new[] { "Idle", "Walk", "Attack", "Cast", "Laugh", "Hit", "Die" })
            {
                Assert.IsTrue(states.ContainsKey(s), "state " + s);
                Assert.IsNotNull(states[s].motion, s + " has its clip");
            }
            var parameters = ac.parameters.Select(p => p.name).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "IsMoving", "Attack", "Cast", "Laugh", "TakeHit", "Die" }, parameters);
            var swap = states["Laugh"].behaviours.OfType<StateMaterialSwap>().FirstOrDefault();
            Assert.IsNotNull(swap, "she grins while she laughs");
            Assert.AreEqual("M_MirrorTwin_Grin", swap.stateMaterial.name);
        }

        [Test]
        public void Boss_FightsWithTridentAndLightning()
        {
            var go = new GameObject("Twin");
            try
            {
                go.AddComponent<StatusEffectController>();
                MirrorTwinBoss twin = go.AddComponent<MirrorTwinBoss>();
                twin.InitializeUnit();
                Assert.AreEqual(MirrorTwinBoss.DisplayName, twin.UnitName);
                Assert.AreEqual(58, twin.MaxHP);
                Assert.AreEqual(15, twin.ArmorClass);
                Assert.AreEqual("1d10+3", twin.DamageFormula, "the trident thrust");
                Assert.IsFalse(twin.CanSurrender, "she never yields");
                for (int i = 0; i < 50; i++)
                {
                    int d = twin.RollAttackDamage();
                    Assert.That(d, Is.InRange(4, 13));
                }
                Assert.IsFalse(twin.IsEnraged);
                twin.TakeDamage(29);
                Assert.IsTrue(twin.IsEnraged, "below half health her trident blazes");
                Assert.AreEqual(0, twin.LightningCooldown, "lightning ready at once");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Tower_MirrorRoomHoldsTheEncounter()
        {
            EditorSceneManager.OpenScene(TowerMirrorSetup.ScenePath, OpenSceneMode.Single);
            MirrorTwinEncounter enc = Object.FindAnyObjectByType<MirrorTwinEncounter>(FindObjectsInactive.Include);
            Assert.IsNotNull(enc, "the encounter is in the Tower");
            Assert.IsNotNull(enc.GetComponentInParent<TowerMirrorRoom>(true), "inside the hidden room, so it waits for the challenge");
            Assert.IsNotNull(enc.Mirror, "it watches the great mirror");
            Assert.IsNotNull(enc.Twin);
            Assert.IsFalse(enc.Twin.gameObject.activeSelf, "the twin hides until the mirror wakes");
            Assert.IsNotNull(enc.Laugh, "the laugh");
            Assert.IsNotNull(enc.EmergePoint);
            Assert.IsNotNull(enc.StandPoint);
            Assert.Greater(enc.EmergePoint.position.z, enc.StandPoint.position.z + 2f, "she steps out toward the room");
            Assert.AreEqual("MirrorTwin", enc.Room.bossIdentifier);
            Assert.AreEqual("MirrorTwin", enc.Room.roomKey);
            CollectionAssert.Contains(enc.Room.roomEnemies, enc.Twin.gameObject);
            BoxCollider trigger = enc.Room.GetComponent<BoxCollider>();
            Assert.IsTrue(trigger == null || enc.Room.transform.TransformPoint(trigger.center).y < -5f, "no walk-in trigger: the mirror starts the fight");
            Assert.Greater(enc.Room.gridAreaXZ.height, 12 * 1.6f, "the grid fits the room");

            TridentLightning lightning = enc.Twin.GetComponentInChildren<TridentLightning>(true);
            Assert.IsNotNull(lightning, "lightning crackles on the trident");
            Assert.AreEqual("Trident_Head", lightning.Head.name);
            Assert.AreEqual(3, lightning.Arcs.Length);
        }

        [Test]
        public void Quest_IsSecretUntilWon()
        {
            QuestSO quest = MirrorTwinQuest.Create();
            Assert.AreEqual(MirrorTwinQuest.QuestId, quest.QuestID);
            Assert.AreEqual(QuestState.NotStarted, quest.DefaultState, "nothing in the tracker until it is won");
            Assert.AreEqual(MirrorTwinQuest.RewardGold, quest.RewardGold);
            Assert.AreEqual("Beware my evil twin on top of the magic tower, traveler.", MirrorTwinQuest.WarningLine);
            Object.DestroyImmediate(quest);
        }

        [Test]
        public void Tavern_BlondeElfGivesTheWarning()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + TavernEntrance.SceneName + ".unity", OpenSceneMode.Single);
            TavernElfWarning warning = TavernElfWarning.Attach();
            Assert.IsNotNull(warning, "the blonde elf stands in the tavern");
            Assert.IsNotNull(warning.GetComponent<Collider>(), "she can be clicked");
            Assert.AreSame(warning, TavernElfWarning.Attach(), "attached once");
        }

        [Test]
        public void Music_TwinFightsToTheWitchsTrack()
        {
            var go = new GameObject("Music");
            try
            {
                MusicManager music = go.AddComponent<MusicManager>();
                Assert.AreEqual(MusicTrackType.MalakorCombat, music.ResolveCombatTrack("MirrorTwin", "Tower"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
