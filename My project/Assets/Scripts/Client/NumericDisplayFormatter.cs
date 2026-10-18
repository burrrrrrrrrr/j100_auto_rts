using System;
using System.Globalization;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// Pure numeric display formatter for the placeholder HUD (Requirements 10.3, 10.4). It
    /// turns a player's resource total and a base's health into the integer strings the client
    /// renders, clamping each to its documented range so the rendered value is always a valid
    /// integer within bounds. No Unity dependency, so the clamping and formatting behaviour is
    /// verifiable out-of-tree; the thin renderer assigns the returned string to a
    /// <c>UnityEngine.UI.Text</c>/<c>TMP_Text</c>.
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <b>Resource total (Requirement 10.3):</b> displayed as an integer in [0, 999,999,999].
    /// Values outside the range clamp to the nearest bound.
    /// </description></item>
    /// <item><description>
    /// <b>Base health (Requirement 10.4):</b> displayed as an integer in [0, maxHealth]. The
    /// (possibly fractional) canonical health is truncated toward zero to an integer and clamped
    /// into [0, floor(maxHealth)], so the shown value never exceeds the base's maximum.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// Formatting uses the invariant culture with no group separators, giving a stable
    /// digit-only string (e.g. <c>"999999999"</c>) regardless of the machine's locale.
    /// </para>
    /// </summary>
    public static class NumericDisplayFormatter
    {
        /// <summary>The documented lower bound for a displayed resource total (Requirement 10.3).</summary>
        public const int ResourceMin = 0;

        /// <summary>The documented upper bound for a displayed resource total (Requirement 10.3).</summary>
        public const int ResourceMax = 999_999_999;

        /// <summary>
        /// Clamps <paramref name="resourceTotal"/> into the documented display range
        /// [0, 999,999,999] (Requirement 10.3). Pure integer result for callers that want the
        /// number before formatting.
        /// </summary>
        public static int ClampResourceTotal(int resourceTotal)
        {
            if (resourceTotal < ResourceMin)
            {
                return ResourceMin;
            }

            return resourceTotal > ResourceMax ? ResourceMax : resourceTotal;
        }

        /// <summary>
        /// Formats <paramref name="resourceTotal"/> as an integer string within
        /// [0, 999,999,999] (Requirement 10.3), clamping out-of-range inputs to the nearest bound.
        /// </summary>
        public static string FormatResourceTotal(int resourceTotal)
        {
            return ClampResourceTotal(resourceTotal).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Clamps <paramref name="currentHealth"/> into the documented display range
        /// [0, floor(maxHealth)] as an integer (Requirement 10.4). The canonical health is
        /// truncated toward zero before clamping; a non-positive or non-finite
        /// <paramref name="maxHealth"/> yields an upper bound of 0, so the result is always a
        /// valid integer that never exceeds the base's maximum.
        /// </summary>
        public static int ClampBaseHealth(float currentHealth, float maxHealth)
        {
            int upper = ToDisplayCeilingMax(maxHealth);

            if (float.IsNaN(currentHealth) || currentHealth <= 0f)
            {
                return 0;
            }

            // Truncate toward zero: a partially-damaged base shows whole points and never rounds
            // up past its real health.
            float truncated = (float)Math.Floor(currentHealth);
            if (truncated <= 0f)
            {
                return 0;
            }

            if (truncated >= upper)
            {
                return upper;
            }

            return (int)truncated;
        }

        /// <summary>
        /// Formats base health as an integer string within [0, floor(maxHealth)] (Requirement
        /// 10.4), clamping and truncating per <see cref="ClampBaseHealth"/>.
        /// </summary>
        public static string FormatBaseHealth(float currentHealth, float maxHealth)
        {
            return ClampBaseHealth(currentHealth, maxHealth).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts a (possibly fractional) max health to the integer display ceiling: floor of a
        /// finite positive value, or 0 when the max is non-positive or non-finite. Also capped at
        /// <see cref="int.MaxValue"/> to stay in range.
        /// </summary>
        private static int ToDisplayCeilingMax(float maxHealth)
        {
            if (float.IsNaN(maxHealth) || maxHealth <= 0f)
            {
                return 0;
            }

            double floored = Math.Floor(maxHealth);
            if (floored >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)floored;
        }
    }
}
