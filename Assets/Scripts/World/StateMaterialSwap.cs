using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Animator state behaviour that shows another material on the character while the state plays,
    /// e.g. the blonde elf's grinning face texture during her Smug clip. The renderer's own material
    /// comes back when the state exits.
    /// </summary>
    public class StateMaterialSwap : StateMachineBehaviour
    {
        [Tooltip("Material shown while this state plays.")]
        public Material stateMaterial;

        [Tooltip("Renderer to swap by name; empty = the first SkinnedMeshRenderer under the Animator.")]
        public string rendererName = "";

        Renderer target;
        Material original;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (stateMaterial == null) return;
            if (target == null) target = FindRenderer(animator);
            if (target == null) return;
            if (target.sharedMaterial != stateMaterial) original = target.sharedMaterial;
            target.sharedMaterial = stateMaterial;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (target != null && original != null) target.sharedMaterial = original;
        }

        Renderer FindRenderer(Animator animator)
        {
            var skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (string.IsNullOrEmpty(rendererName)) return skins.Length > 0 ? skins[0] : null;
            foreach (var r in skins)
                if (r.name == rendererName) return r;
            return null;
        }
    }
}
