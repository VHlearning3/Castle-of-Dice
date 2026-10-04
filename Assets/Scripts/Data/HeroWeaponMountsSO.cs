using System;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Data
{
    /// <summary>
    /// Which weapon each hero class holds and where it sits on the hand bone (Elira's staff, Corvo's daggers).
    /// Loaded from Resources/HeroWeaponMounts; the offsets are baked from the Blender fit by
    /// CastleOfDice/Setup Low-Poly Items, so re-run that rather than typing numbers in by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "HeroWeaponMounts", menuName = "CastleOfDice/Data/Hero Weapon Mounts", order = 21)]
    public class HeroWeaponMountsSO : ScriptableObject
    {
        [Serializable]
        public class Mount
        {
            public CharacterClassType heroClass;
            [Tooltip("Mesh prefab whose pivot is the grip.")]
            public GameObject weaponPrefab;
            [Tooltip("Bone the weapon hangs from, e.g. mixamorig:RightHand.")]
            public string boneName;
            public Vector3 localPosition;
            public Quaternion localRotation = Quaternion.identity;
            public Vector3 localScale = Vector3.one;
        }

        public Mount[] mounts = new Mount[0];

        private const string ResourcePath = "HeroWeaponMounts";
        private static HeroWeaponMountsSO s_instance;
        private static bool s_loaded;

        public static HeroWeaponMountsSO Instance
        {
            get
            {
                if (!s_loaded)
                {
                    s_instance = Resources.Load<HeroWeaponMountsSO>(ResourcePath);
                    s_loaded = true;
                }
                return s_instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_loaded = false;
            s_instance = null;
        }

        /// <summary>
        /// Hangs the weapons of <paramref name="heroClass"/> on the matching bones under <paramref name="model"/>.
        /// Safe to call again: a weapon already on its bone is left alone.
        /// </summary>
        public void AttachTo(GameObject model, CharacterClassType heroClass)
        {
            if (model == null) return;
            for (int i = 0; i < mounts.Length; i++)
            {
                Mount m = mounts[i];
                if (m == null || m.heroClass != heroClass || m.weaponPrefab == null) continue;

                Transform bone = FindDeep(model.transform, m.boneName);
                if (bone == null)
                {
                    Debug.LogWarning($"[HeroWeaponMounts] Bone '{m.boneName}' not found under '{model.name}'.");
                    continue;
                }
                if (bone.Find(m.weaponPrefab.name) != null) continue;

                GameObject weapon = Instantiate(m.weaponPrefab, bone, false);
                weapon.name = m.weaponPrefab.name;
                weapon.transform.localPosition = m.localPosition;
                weapon.transform.localRotation = m.localRotation;
                weapon.transform.localScale = m.localScale;
            }
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
