using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The blonde elf prefab: Humanoid avatar, her five clips in the controller, and the grinning face
    /// material that the Smug state shows while it plays.
    /// </summary>
    [TestFixture]
    public class BlondeElfTests
    {
        const string PrefabPath = "Assets/Characters/BlondeElf/BlondeElf.prefab";
        const string ControllerPath = "Assets/Characters/Animators/BlondeElf_Animator.controller";

        [Test]
        public void Prefab_IsHumanoidWithAllClips()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            var animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(animator.avatar);
            Assert.IsTrue(animator.avatar.isHuman && animator.avatar.isValid);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.AreSame(ctrl, animator.runtimeAnimatorController);
            var states = ctrl.layers[0].stateMachine.states.Select(s => s.state).ToDictionary(s => s.name);
            foreach (var name in new[] { "Idle", "Walk", "Smug", "Attack", "Die" })
            {
                Assert.IsTrue(states.ContainsKey(name), name);
                Assert.IsNotNull(states[name].motion, name);
            }
            Assert.IsTrue(ctrl.parameters.Any(p => p.name == "Smug" && p.type == AnimatorControllerParameterType.Trigger));
        }

        [Test]
        public void SmugState_ShowsGrinMaterialAndRestoresIt()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var smug = ctrl.layers[0].stateMachine.states.Select(s => s.state).First(s => s.name == "Smug");
            var swapAsset = smug.behaviours.OfType<StateMaterialSwap>().Single();
            Assert.IsNotNull(swapAsset.stateMaterial);
            StringAssert.Contains("Grin", swapAsset.stateMaterial.GetTexture("_BaseMap").name);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var swap = ScriptableObject.CreateInstance<StateMaterialSwap>();
            try
            {
                swap.stateMaterial = swapAsset.stateMaterial;
                var animator = go.GetComponent<Animator>();
                var body = go.GetComponentInChildren<SkinnedMeshRenderer>();
                var normal = body.sharedMaterial;
                Assert.AreNotSame(swap.stateMaterial, normal);

                swap.OnStateEnter(animator, default, 0);
                Assert.AreSame(swap.stateMaterial, body.sharedMaterial);
                swap.OnStateExit(animator, default, 0);
                Assert.AreSame(normal, body.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(swap);
                Object.DestroyImmediate(go);
            }
        }
    }
}
