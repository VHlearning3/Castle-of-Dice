using System;
using System.Reflection;
using UnityEngine;

namespace CastleOfTheD20.Tests.E2E.Common
{
    /// <summary>
    /// Decoupled Test Driver for MusicManager.
    /// Provides reflection-safe binding so tests compile cleanly before, during,
    /// and after Milestone M1 implementation, while enabling deep inspection of AudioSources,
    /// volume curves, and event responses.
    /// </summary>
    public class MusicManagerTestDriver
    {
        private static Type s_musicManagerType;
        private static Type s_musicTrackType;

        public static Type MusicManagerType
        {
            get
            {
                if (s_musicManagerType == null)
                {
                    s_musicManagerType = Type.GetType("CastleOfTheD20.Core.MusicManager, Assembly-CSharp")
                                      ?? Type.GetType("CastleOfTheD20.Core.MusicManager, Assembly-CSharp-firstpass")
                                      ?? Type.GetType("CastleOfTheD20.Core.MusicManager");
                }
                return s_musicManagerType;
            }
        }

        public static Type MusicTrackType
        {
            get
            {
                if (s_musicTrackType == null)
                {
                    s_musicTrackType = Type.GetType("CastleOfTheD20.Core.MusicTrackType, Assembly-CSharp")
                                    ?? Type.GetType("CastleOfTheD20.Core.MusicTrackType, Assembly-CSharp-firstpass")
                                    ?? Type.GetType("CastleOfTheD20.Core.MusicTrackType");
                }
                return s_musicTrackType;
            }
        }

        public static bool IsImplemented => MusicManagerType != null;
        public static bool IsTrackTypeImplemented => MusicTrackType != null;

        private GameObject driverHolder;
        private MonoBehaviour musicManagerInstance;

        public MonoBehaviour Instance
        {
            get
            {
                if ((musicManagerInstance == null || !musicManagerInstance) && IsImplemented)
                {
                    PropertyInfo instProp = MusicManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instProp != null)
                    {
                        var val = instProp.GetValue(null) as MonoBehaviour;
                        if (val != null && val)
                        {
                            musicManagerInstance = val;
                        }
                        else
                        {
                            musicManagerInstance = null;
                        }
                    }
                }
                return musicManagerInstance;
            }
        }

        /// <summary>
        /// Creates a clean test instance of MusicManager if not already active.
        /// </summary>
        public MonoBehaviour SetupTestInstance()
        {
            if (!IsImplemented) return null;

            if (Instance == null)
            {
                driverHolder = new GameObject("TEST_MusicManager_Holder");
                musicManagerInstance = driverHolder.AddComponent(MusicManagerType) as MonoBehaviour;
            }

            if (Instance != null)
            {
                MethodInfo initMethod = MusicManagerType.GetMethod("InitializeAudioSources", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (initMethod != null)
                {
                    initMethod.Invoke(Instance, null);
                }
            }
            return Instance;
        }

        /// <summary>
        /// Cleans up test GameObjects and resets singleton.
        /// </summary>
        public void Teardown()
        {
            if (driverHolder != null)
            {
                UnityEngine.Object.DestroyImmediate(driverHolder);
                driverHolder = null;
            }
            else if (musicManagerInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(musicManagerInstance.gameObject);
            }
            musicManagerInstance = null;

            // Reset static instance via reflection if needed
            if (IsImplemented)
            {
                PropertyInfo instProp = MusicManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instProp != null)
                {
                    MethodInfo setMethod = instProp.GetSetMethod(true);
                    if (setMethod != null)
                    {
                        setMethod.Invoke(null, new object[] { null });
                    }
                    else if (instProp.CanWrite)
                    {
                        instProp.SetValue(null, null);
                    }
                }
            }
        }

        public void PlayMusic(AudioClip clip, float fadeDuration = 1.2f)
        {
            if (Instance == null) return;
            MethodInfo method = MusicManagerType.GetMethod("PlayMusic", new[] { typeof(AudioClip), typeof(float) })
                             ?? MusicManagerType.GetMethod("PlayMusic", new[] { typeof(AudioClip) });
            if (method != null)
            {
                if (method.GetParameters().Length == 2)
                    method.Invoke(Instance, new object[] { clip, fadeDuration });
                else
                    method.Invoke(Instance, new object[] { clip });
            }
        }

        public void PlayTrack(string trackTypeName, float fadeDuration = 1.2f)
        {
            if (Instance == null || MusicTrackType == null) return;
            object enumVal = Enum.Parse(MusicTrackType, trackTypeName, true);
            MethodInfo method = MusicManagerType.GetMethod("PlayTrack", new[] { MusicTrackType, typeof(float) })
                             ?? MusicManagerType.GetMethod("PlayTrack", new[] { MusicTrackType });
            if (method != null)
            {
                if (method.GetParameters().Length == 2)
                    method.Invoke(Instance, new object[] { enumVal, fadeDuration });
                else
                    method.Invoke(Instance, new object[] { enumVal });
            }
        }

        public void PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")
        {
            if (Instance == null) return;
            MethodInfo method = MusicManagerType.GetMethod("PlayCombatMusicForBoss", new[] { typeof(string), typeof(string) });
            if (method != null)
            {
                method.Invoke(Instance, new object[] { bossIdentifier, roomLocation });
            }
        }

        public void RestoreExplorationMusic(float delay = 1.5f)
        {
            if (Instance == null) return;
            MethodInfo method = MusicManagerType.GetMethod("RestoreExplorationMusic", new[] { typeof(float) })
                             ?? MusicManagerType.GetMethod("RestoreExplorationMusic", Type.EmptyTypes);
            if (method != null)
            {
                if (method.GetParameters().Length == 1)
                    method.Invoke(Instance, new object[] { delay });
                else
                    method.Invoke(Instance, null);
            }
        }

        public void StopMusic(float fadeDuration = 1.2f)
        {
            if (Instance == null) return;
            MethodInfo method = MusicManagerType.GetMethod("StopMusic", new[] { typeof(float) })
                             ?? MusicManagerType.GetMethod("StopMusic", Type.EmptyTypes);
            if (method != null)
            {
                if (method.GetParameters().Length == 1)
                    method.Invoke(Instance, new object[] { fadeDuration });
                else
                    method.Invoke(Instance, null);
            }
        }

        public void SetVolume(float master, float music)
        {
            if (Instance == null) return;
            MethodInfo method = MusicManagerType.GetMethod("SetVolume", new[] { typeof(float), typeof(float) });
            if (method != null)
            {
                method.Invoke(Instance, new object[] { master, music });
            }
            else
            {
                PropertyInfo masterProp = MusicManagerType.GetProperty("MasterVolume");
                PropertyInfo musicProp = MusicManagerType.GetProperty("MusicVolume");
                if (masterProp != null && masterProp.CanWrite) masterProp.SetValue(Instance, master);
                if (musicProp != null && musicProp.CanWrite) musicProp.SetValue(Instance, music);
            }
        }

        public AudioSource GetSourceA()
        {
            if (Instance == null) return null;
            FieldInfo field = MusicManagerType.GetField("sourceA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                var src = field.GetValue(Instance) as AudioSource;
                if (src != null) return src;
            }
            PropertyInfo prop = MusicManagerType.GetProperty("SourceA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (prop != null) return prop.GetValue(Instance) as AudioSource;
            return null;
        }

        public AudioSource GetSourceB()
        {
            if (Instance == null) return null;
            FieldInfo field = MusicManagerType.GetField("sourceB", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                var src = field.GetValue(Instance) as AudioSource;
                if (src != null) return src;
            }
            PropertyInfo prop = MusicManagerType.GetProperty("SourceB", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (prop != null) return prop.GetValue(Instance) as AudioSource;
            return null;
        }

        public AudioClip GetCurrentClip()
        {
            if (Instance == null) return null;
            PropertyInfo prop = MusicManagerType.GetProperty("CurrentClip");
            if (prop != null) return prop.GetValue(Instance) as AudioClip;
            return null;
        }

        public bool GetIsPlaying()
        {
            if (Instance == null) return false;
            PropertyInfo prop = MusicManagerType.GetProperty("IsPlaying");
            if (prop != null) return (bool)prop.GetValue(Instance);
            return false;
        }

        public float GetMasterVolume()
        {
            if (Instance == null) return 0f;
            PropertyInfo prop = MusicManagerType.GetProperty("MasterVolume");
            if (prop != null) return (float)prop.GetValue(Instance);
            return 0f;
        }

        public float GetMusicVolume()
        {
            if (Instance == null) return 0f;
            PropertyInfo prop = MusicManagerType.GetProperty("MusicVolume");
            if (prop != null) return (float)prop.GetValue(Instance);
            return 0f;
        }

        public static AudioClip LoadAudioClip(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
#else
            return null;
#endif
        }
    }
}
