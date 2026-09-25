using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using CastleOfTheD20.Tests.E2E.Common;

namespace CastleOfTheD20.Tests.E2E.Tier1_FeatureCoverage
{
    /// <summary>
    /// Tier 1 Feature Coverage: Milestone M4 (F4.1, F4.2, F4.3)
    /// Editor Tooling, Scene Setup, and Asset GUID Preservation.
    /// </summary>
    public class Tier1_F4_EditorToolingTests : E2ETestSuite
    {
        #region F4.1: BuildVillageEditor Music Setup Tests (2 Tests)

        [E2ETestCase(TestTier.Tier1_FeatureCoverage, "F4.1", "Verify BuildVillageEditor class exists in Editor assembly")]
        public void F4_1_01_BuildVillageEditor_ClassExists()
        {
            Type editorType = Type.GetType("CastleOfTheD20.Editor.BuildVillageEditor, Assembly-CSharp-Editor")
                           ?? Type.GetType("CastleOfTheD20.Editor.BuildVillageEditor");

            E2EAudioAssert.IsNotNull(editorType, "BuildVillageEditor class must exist in CastleOfTheD20.Editor");
        }

        [E2ETestCase(TestTier.Tier1_FeatureCoverage, "F4.1", "Verify StartVillage scene asset exists in Assets/Scenes/")]
        public void F4_1_02_StartVillageScene_Exists()
        {
            string scenePath = Path.Combine(Application.dataPath, "Scenes", "StartVillage.unity");
            E2EAudioAssert.IsTrue(File.Exists(scenePath), $"StartVillage.unity scene must exist at {scenePath}");
        }

        #endregion

        #region F4.2: Control Window Integration Tests (1 Test)

        [E2ETestCase(TestTier.Tier1_FeatureCoverage, "F4.2", "Verify CastleOfDiceControlWindow class exists and exposes ShowWindow")]
        public void F4_2_01_CastleOfDiceControlWindow_Exists()
        {
            Type windowType = Type.GetType("CastleOfTheD20.Editor.CastleOfDiceControlWindow, Assembly-CSharp-Editor")
                           ?? Type.GetType("CastleOfTheD20.Editor.CastleOfDiceControlWindow");

            E2EAudioAssert.IsNotNull(windowType, "CastleOfDiceControlWindow must exist in CastleOfTheD20.Editor");

            MethodInfo showMethod = windowType.GetMethod("ShowWindow", BindingFlags.Public | BindingFlags.Static);
            E2EAudioAssert.IsNotNull(showMethod, "CastleOfDiceControlWindow must have public static ShowWindow method");
        }

        #endregion

        #region F4.3: GUID & .meta Preservation Tests (2 Tests)

        [E2ETestCase(TestTier.Tier1_FeatureCoverage, "F4.3", "Verify all 7 audio meta files exist")]
        public void F4_3_01_AudioMetaFiles_AllExist()
        {
            string[] requiredTracks = new[]
            {
                "VillageSong.mp3.meta",
                "Castle_adventure_song.mp3.meta",
                "Cellar_combat_music.mp3.meta",
                "CursedCommander_Combat_music.mp3.meta",
                "Malakor_combat_music.mp3.meta",
                "1_Combat_GargoyleKing_music.mp3.meta",
                "2_Combat_GargoyleKing_music.mp3.meta"
            };

            string musicDir = Path.Combine(Application.dataPath, "Music");
            foreach (string metaFile in requiredTracks)
            {
                string path = Path.Combine(musicDir, metaFile);
                E2EAudioAssert.IsTrue(File.Exists(path), $"Audio meta file missing: {metaFile}");

                string content = File.ReadAllText(path);
                E2EAudioAssert.IsTrue(content.Contains("guid:"), $"Meta file {metaFile} must contain a valid guid entry");
            }
        }

        [E2ETestCase(TestTier.Tier1_FeatureCoverage, "F4.3", "Verify known GUIDs for key tracks remain uncorrupted")]
        public void F4_3_02_KnownGUIDs_Preserved()
        {
            // Verify specific known GUIDs observed from project metadata
            string musicDir = Path.Combine(Application.dataPath, "Music");

            string villageMeta = File.ReadAllText(Path.Combine(musicDir, "VillageSong.mp3.meta"));
            E2EAudioAssert.IsTrue(villageMeta.Contains("8c695f2c8079fff438568e6ed65231a4"),
                "VillageSong.mp3 GUID must remain exactly 8c695f2c8079fff438568e6ed65231a4");

            string commanderMeta = File.ReadAllText(Path.Combine(musicDir, "CursedCommander_Combat_music.mp3.meta"));
            E2EAudioAssert.IsTrue(commanderMeta.Contains("af6d1f5f2a827c84d8ab7e04b82fab99"),
                "CursedCommander_Combat_music.mp3 GUID must remain exactly af6d1f5f2a827c84d8ab7e04b82fab99");

            string malakorMeta = File.ReadAllText(Path.Combine(musicDir, "Malakor_combat_music.mp3.meta"));
            E2EAudioAssert.IsTrue(malakorMeta.Contains("64944305ed273854687c0951beb9a69c"),
                "Malakor_combat_music.mp3 GUID must remain exactly 64944305ed273854687c0951beb9a69c");
        }

        #endregion
    }
}
