using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili, 2026-10-10: the Cursed Commander and the skeleton guard carry swords and fight with sword
    /// animations (idle, attack, hit, death); the skeleton models are lighter (decimated mesh).
    /// Set SKELETON_SHOTS_DIR to save review screenshots.
    /// </summary>
    public class SkeletonSwordsPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;

        [SetUp]
        public void BackupSave() => saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;

        [TearDown]
        public void RestoreSave()
        {
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CommanderAndSkeleton_HoldSwords_AndPlaySwordTakes()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Zone_3_CastleCourtyard");
            for (int i = 0; i < 30; i++) yield return null;

            Transform cmd = Find("Boss_CursedCommander"), skel = Find("Courtyard_Skeleton");
            Assert.IsNotNull(cmd, "The Commander is in the courtyard."); Assert.IsNotNull(skel, "The skeleton guard is in the courtyard.");
            foreach (Transform t in new[] { cmd, skel })
            {
                Transform p = t;
                while (p != null) { p.gameObject.SetActive(true); p = p.parent; }
            }
            Transform npc = Find("NPC_CursedCommander");   // the talking Commander before the fight stands in the same spot
            if (npc != null) npc.gameObject.SetActive(false);
            Animator ca =cmd.GetComponentInChildren<Animator>(true), sa = skel.GetComponentInChildren<Animator>(true);
            Vector3 spot = cmd.position;
            skel.position = spot + new Vector3(3.2f, 0f, 0f);
            skel.rotation = cmd.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (Animator a in new[] { ca, sa })
            {
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Assert.IsNotNull(Bone(a.transform, "Right_Hand").Find("Sword"), a.name + " holds a sword in the right hand.");
                var skin = a.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.Less(skin.sharedMesh.triangles.Length / 3, 3000, "The decimated mesh is used.");
            }
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb.GetType().Name == "DungeonRoomController") mb.enabled = false;

            yield return null;
            foreach (Transform t in new[] { cmd, skel })
                foreach (Animator a in t.GetComponentsInChildren<Animator>(true))
                    Debug.Log($"[SkelDiag] {t.name}/{a.name} enabled={a.isActiveAndEnabled} ctrl={a.runtimeAnimatorController?.name} avatar={a.avatar?.name} speed={a.speed} ts={Time.timeScale} swords={a.GetComponentsInChildren<MeshFilter>(true).Length} path={a.transform.parent?.name}");
            Vector3 mid = spot + new Vector3(1.6f, 1.8f, 0f);
            var close = (mid + new Vector3(-1.5f, 1.2f, -6.8f), mid);
            var game = (mid + new Vector3(0f, 9f, -9f), mid + Vector3.down * 0.8f);
            yield return Wait(1.0f);
            Vector3 head = Bone(ca.transform, "Head").position;
            yield return Shot("0_commander_face", (head + cmd.forward * 2.2f + Vector3.up * 0.2f, head));
            yield return Shot("1_idle", close); yield return Shot("1_idle_game", game);
            foreach (Animator a in new[] { ca, sa }) a.SetTrigger("Attack");
            yield return Wait(0.35f); yield return Shot("2_attack_windup", close);
            yield return Wait(0.25f); yield return Shot("3_attack_strike", close);
            yield return Wait(1.2f);
            foreach (Animator a in new[] { ca, sa }) a.SetTrigger("TakeHit");
            yield return Wait(0.2f); yield return Shot("4_hit", close);
            AnimatorStateInfo st = ca.GetCurrentAnimatorStateInfo(0);
            Assert.IsTrue(st.IsName("Hit") || ca.IsInTransition(0), "TakeHit plays the hit take.");
            yield return Wait(1.0f);
            Assert.IsTrue(ca.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "The hit take returns to idle.");
            foreach (Animator a in new[] { ca, sa }) a.SetTrigger("Die");
            yield return Wait(0.8f); yield return Shot("5_die_fall", close);
            yield return Wait(1.8f); yield return Shot("6_die_down", close);
        }

        private static IEnumerator Wait(float s)
        {
            float until = Time.time + s;
            while (Time.time < until) yield return null;
        }

        private static Transform Bone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private static Transform Find(string name)
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == name && t.gameObject.scene.IsValid()) return t;
            return null;
        }

        private static IEnumerator Shot(string name, (Vector3 pos, Vector3 look) v)
        {
            string dir = System.Environment.GetEnvironmentVariable("SKELETON_SHOTS_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            Directory.CreateDirectory(dir);
            Camera cam = Camera.main;
            foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>()) if (mb.GetType().Name == "CameraFollow") mb.enabled = false;
            cam.transform.position = v.pos;
            cam.transform.LookAt(v.look);
            yield return null;
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.Destroy(rt); Object.Destroy(tex);
        }
    }
}
