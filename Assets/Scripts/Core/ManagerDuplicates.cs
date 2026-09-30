using UnityEngine;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Shared duplicate handling for the persistent managers.
    /// </summary>
    public static class ManagerDuplicates
    {
        /// <summary>
        /// Removes a duplicate manager. A GameObject that also carries other components (such as the
        /// combined "Managers" object) keeps them; only the duplicate component goes. A GameObject that
        /// holds nothing but the duplicate is removed entirely.
        /// </summary>
        public static void Discard(Component duplicate)
        {
            if (duplicate == null) return;

            if (duplicate.GetComponents<Component>().Length > 2)
            {
                Object.Destroy(duplicate);
            }
            else
            {
                Object.Destroy(duplicate.gameObject);
            }
        }
    }
}
