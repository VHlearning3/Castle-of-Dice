namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// A boss whose room allies stay out of the fight until the boss calls them in (the Cursed Commander's
    /// skeleton guard arrives at 50% HP). The room keeps those allies hidden when the encounter starts and
    /// hands them to the boss as reserves.
    /// </summary>
    public interface IReinforcementSummoner
    {
        /// <summary>True when <paramref name="ally"/> should wait in reserve instead of starting the fight.</summary>
        bool HoldsBackAtStart(EnemyUnit ally);

        /// <summary>Gives the boss a hidden ally to call in later.</summary>
        void AddReserve(EnemyUnit ally);
    }
}
