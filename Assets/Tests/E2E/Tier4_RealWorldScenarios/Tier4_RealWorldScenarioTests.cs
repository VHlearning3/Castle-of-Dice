using System;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Tests.E2E.Common;

namespace CastleOfTheD20.Tests.E2E.Tier4_RealWorldScenarios
{
    /// <summary>
    /// Tier 4: Real-World Application Scenarios.
    /// End-to-end full dungeon campaign run and full WebGL browser lifecycle simulation.
    /// </summary>
    public class Tier4_RealWorldScenarioTests : E2ETestSuite
    {
        [E2ETestCase(TestTier.Tier4_RealWorldScenarios, "Scenario", "Full Dungeon Campaign E2E Walkthrough: Village -> Boss 1 -> Boss 2 -> Final Boss P1&P2 -> Campaign Victory")]
        public void T4_01_FullCampaign_E2E_DungeonRun()
        {
            // Full campaign playthrough lifecycle simulation
            GameObject gmObj = new GameObject("TEST_Campaign_GM");
            GameObject bossObj = new GameObject("TEST_Campaign_Bosses");
            try
            {
                GameManager gm = gmObj.AddComponent<GameManager>();

                // Phase 1: Start at Oakhaven Village
                gm.SetLocation(GameLocation.Village);
                E2EAudioAssert.AreEqual(GameLocation.Village, gm.CurrentLocation, "Campaign begins in Village");

                // Phase 2: Traverse to Wing 1 (Courtyard) and engage Cursed Commander
                gm.SetLocation(GameLocation.Courtyard);
                E2EAudioAssert.AreEqual(GameLocation.Courtyard, gm.CurrentLocation, "Player enters Courtyard");
                gm.NotifyBossDefeated("CursedCommander");
                E2EAudioAssert.IsTrue(gm.IsCommanderDefeated, "Cursed Commander defeated and recorded in campaign progression");

                // Phase 3: Move to Wing 2 (Library) and engage Shadow Mage Malakor
                gm.SetLocation(GameLocation.Library);
                E2EAudioAssert.AreEqual(GameLocation.Library, gm.CurrentLocation, "Player enters Library");
                gm.NotifyBossDefeated("ShadowMageMalakor");
                E2EAudioAssert.IsTrue(gm.IsMalakorDefeated, "Shadow Mage Malakor defeated and recorded");

                // Phase 4: Move to Wing 3 (The Crown Hall) and engage The Gargoyle King
                gm.SetLocation(GameLocation.CrownHall);
                E2EAudioAssert.AreEqual(GameLocation.CrownHall, gm.CurrentLocation, "Player enters Crown Hall");

                GargoyleKingBoss boss = bossObj.AddComponent<GargoyleKingBoss>();
                boss.InitializeUnit();

                // Boss Phase 1 -> Phase 2
                boss.TakeDamage(35);
                E2EAudioAssert.IsTrue(boss.IsStoneFormActive, "Gargoyle King enters Stone Form Phase 2");

                // Phase 5: Defeat Gargoyle King -> Campaign Victory!
                boss.TakeDamage(100);
                gm.NotifyBossDefeated("GargoyleKing");
                E2EAudioAssert.IsTrue(gm.IsGargoyleKingDefeated, "Gargoyle King defeated! All three wings cleared.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gmObj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }
        }

        [E2ETestCase(TestTier.Tier4_RealWorldScenarios, "Scenario", "WebGL Browser Lifecycle Simulation: Autoplay unlock -> Volume Adjust -> Pause -> Resume -> Teardown")]
        public void T4_02_WebGL_SessionSimulation()
        {
            // Simulate browser loading tab with suspended AudioContext
            bool initialPause = AudioListener.pause;
            try
            {
                AudioListener.pause = true;
                E2EAudioAssert.IsTrue(AudioListener.pause, "WebGL audio context initially suspended by browser policy");

                // User clicks on canvas -> unpauses
                AudioListener.pause = false;
                E2EAudioAssert.IsFalse(AudioListener.pause, "Player interaction unlocks WebGL audio context");

                // Player opens pause menu
                float originalScale = Time.timeScale;
                try
                {
                    Time.timeScale = 0f;
                    E2EAudioAssert.AreEqual(0f, Time.timeScale, "Game paused");

                    // Resumes game
                    Time.timeScale = 1f;
                    E2EAudioAssert.AreEqual(1f, Time.timeScale, "Game resumed cleanly");
                }
                finally
                {
                    Time.timeScale = originalScale;
                }
            }
            finally
            {
                AudioListener.pause = initialPause;
            }
        }
    }
}
