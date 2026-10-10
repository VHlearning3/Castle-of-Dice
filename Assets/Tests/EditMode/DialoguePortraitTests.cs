using NUnit.Framework;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>Every name that speaks in a dialogue box finds its own face icon (Vili, 2026-10-10).</summary>
    public class DialoguePortraitTests
    {
        [TestCase("Baldur the Smith", "Baldur")]
        [TestCase("Baldur", "Baldur")]
        [TestCase("Elder Othelia", "Othelia")]
        [TestCase("Lady Othelia", "Othelia")]
        [TestCase("Innkeeper Barnaby", "Barnaby")]
        [TestCase("Mirabel the Herbalist", "Mirabel")]
        [TestCase(HubDialogues.MerchantName, "Pip")]
        [TestCase(HubDialogues.GhostName, "GhostAldric")]
        [TestCase(TavernElfWarning.SpeakerName, "BlondeElf")]
        [TestCase(SurrenderDialogues.CultistSpeaker, "Cultist")]
        [TestCase("Cursed Commander", "CursedCommander")]
        [TestCase(SurrenderDialogues.WitchSpeaker, "ShadowWitch")]
        [TestCase("The Gargoyle King", "GargoyleKing")]
        [TestCase(MirrorTwinBoss.DisplayName, "MirrorTwin")]
        public void Speaker_HasOwnFace(string speaker, string key)
        {
            Assert.AreEqual(key, DialoguePortraits.KeyFor(speaker));
            Assert.IsNotNull(DialoguePortraits.For(speaker), $"Resources/Portraits/{key}.png loads as a sprite.");
        }

        [Test]
        public void UnknownSpeaker_KeepsThePlaceholder()
        {
            Assert.IsNull(DialoguePortraits.For("Village NPC"));
            Assert.IsNull(DialoguePortraits.For(""));
        }
    }
}
