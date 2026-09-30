using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>Outcome of one press in the lockpick minigame.</summary>
    public enum LockpickPressResult
    {
        /// <summary>The minigame is already over; the press did nothing.</summary>
        Ignored,

        /// <summary>The marker was in the gold zone: one pin is set, more remain.</summary>
        PinSet,

        /// <summary>The marker missed the gold zone: one slip counted, the pin stays down.</summary>
        Slipped,

        /// <summary>The last pin was set: the lock is open.</summary>
        Unlocked,

        /// <summary>Too many slips: the attempt failed (the lock's trap may spring).</summary>
        Broken
    }

    /// <summary>
    /// Pure timing logic for the Rogue lockpick minigame (no UI, no randomness beyond the injected RNG).
    /// A marker slides back and forth along a 0..1 bar; pressing while it is inside the gold zone sets a pin.
    /// Set every pin to open the lock; slipping too many times fails the attempt.
    /// The lock's DC sets the difficulty: a higher DC narrows the gold zone and speeds up the marker.
    /// </summary>
    public sealed class LockpickMinigame
    {
        public const int DefaultPinCount = 3;
        public const int DefaultMaxSlips = 2;

        /// <summary>Each set pin makes the next one this much faster (0.15 = +15% per pin).</summary>
        public const float SpeedGainPerPin = 0.15f;

        private const float EdgeMargin = 0.04f;
        private const float MinCenterDistanceFromMarker = 0.25f;

        private readonly System.Random rng;
        private readonly float baseSpeed;
        private int direction = 1;

        public int PinCount { get; }
        public int MaxSlips { get; }
        public int PinsSet { get; private set; }
        public int Slips { get; private set; }

        /// <summary>Width of the gold zone as a fraction of the bar (0..1).</summary>
        public float SweetZoneWidth { get; }

        /// <summary>Centre of the current pin's gold zone on the bar (0..1).</summary>
        public float SweetZoneCenter { get; private set; }

        /// <summary>Marker position on the bar (0..1).</summary>
        public float MarkerPosition { get; private set; }

        /// <summary>Marker speed in bar lengths per second for the current pin.</summary>
        public float MarkerSpeed => baseSpeed * (1f + SpeedGainPerPin * PinsSet);

        public bool IsFinished { get; private set; }
        public bool IsUnlocked { get; private set; }

        public bool IsMarkerInSweetZone => Mathf.Abs(MarkerPosition - SweetZoneCenter) <= SweetZoneWidth * 0.5f;

        public LockpickMinigame(int difficultyClass, int pinCount = DefaultPinCount, int maxSlips = DefaultMaxSlips, System.Random random = null)
        {
            PinCount = Mathf.Max(1, pinCount);
            MaxSlips = Mathf.Max(1, maxSlips);
            SweetZoneWidth = SweetZoneWidthForDC(difficultyClass);
            baseSpeed = MarkerSpeedForDC(difficultyClass);
            rng = random ?? new System.Random();

            MarkerPosition = 0f;
            PlaceSweetZone();
        }

        /// <summary>Gold zone width for a lock DC: DC 8 = 36% of the bar, DC 13 = 21%, DC 17+ = 10%.</summary>
        public static float SweetZoneWidthForDC(int difficultyClass)
        {
            return Mathf.Clamp(0.36f - (difficultyClass - 8) * 0.03f, 0.10f, 0.36f);
        }

        /// <summary>Marker speed for a lock DC in bar lengths per second: DC 8 = 0.6, DC 13 = 1.0, capped at 1.8.</summary>
        public static float MarkerSpeedForDC(int difficultyClass)
        {
            return Mathf.Clamp(0.6f + (difficultyClass - 8) * 0.08f, 0.5f, 1.8f);
        }

        /// <summary>Slides the marker, bouncing off both ends of the bar.</summary>
        public void Tick(float deltaTime)
        {
            if (IsFinished || deltaTime <= 0f) return;

            float pos = MarkerPosition + direction * MarkerSpeed * deltaTime;

            // A large step can bounce more than once; fold it back into 0..1
            while (pos > 1f || pos < 0f)
            {
                if (pos > 1f)
                {
                    pos = 2f - pos;
                    direction = -1;
                }
                else
                {
                    pos = -pos;
                    direction = 1;
                }
            }

            MarkerPosition = pos;
        }

        /// <summary>Tries to set the current pin at the marker's position.</summary>
        public LockpickPressResult Press()
        {
            if (IsFinished) return LockpickPressResult.Ignored;

            if (IsMarkerInSweetZone)
            {
                PinsSet++;
                if (PinsSet >= PinCount)
                {
                    IsFinished = true;
                    IsUnlocked = true;
                    return LockpickPressResult.Unlocked;
                }

                PlaceSweetZone();
                return LockpickPressResult.PinSet;
            }

            Slips++;
            if (Slips >= MaxSlips)
            {
                IsFinished = true;
                return LockpickPressResult.Broken;
            }

            return LockpickPressResult.Slipped;
        }

        /// <summary>Moves the gold zone somewhere new, away from the marker so a pin is never free.</summary>
        private void PlaceSweetZone()
        {
            float half = SweetZoneWidth * 0.5f;
            float min = half + EdgeMargin;
            float max = 1f - half - EdgeMargin;

            float best = min;
            float bestDistance = -1f;
            for (int i = 0; i < 8; i++)
            {
                float candidate = min + (float)rng.NextDouble() * (max - min);
                float distance = Mathf.Abs(candidate - MarkerPosition);
                if (distance >= MinCenterDistanceFromMarker)
                {
                    best = candidate;
                    break;
                }

                if (distance > bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            SweetZoneCenter = best;
        }
    }
}
