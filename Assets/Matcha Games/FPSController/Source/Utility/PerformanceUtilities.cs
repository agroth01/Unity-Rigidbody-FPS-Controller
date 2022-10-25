using System;
using UnityEngine;

namespace URC.Utility
{
    /// <summary>
    /// Various utilities for performance and optimization.
    /// 
    /// For almost all scenarios, using these are overkill and not needed for most projects.
    /// However, since I want to make this as performant as possible, I have included them.
    /// Hopefully some people might learn some tricks for optimization by checking out this.
    /// </summary>
    public static class PerformanceUtilities
    {
        /// <summary>
        /// Will only return true at the given frequency.
        /// 30 = every 30 frames
        /// 60 = every 60 frames.
        /// Useful for scenarios where heavy calculations are required often, but not every frame.
        /// 
        /// Note: Should be used *very* sparingly, as putting all heavy operations to run on the same frame can introduce stutter.
        /// Could probably be optimized by adding an optional delay variable to the call and have it be different each place called in code, but it's not scalable.
        /// </summary>
        /// <param name="frequency"></param>
        /// <returns></returns>
        public static bool LimitRate(int frequency)
        {
            if (Time.frameCount % frequency == 0)
            {
                return true;
            }

            else
            {
                return false;
            }
        }
    }
}
