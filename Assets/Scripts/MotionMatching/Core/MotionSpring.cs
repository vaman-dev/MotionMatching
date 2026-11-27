using System;

namespace MotionMatching.Core
{
    /// <summary>Exact critical spring for a constant goal during a step. Half-life is positional with zero initial velocity.</summary>
    public static class MotionSpring
    {
        public static void Step(ref float value, ref float velocity, float goal, float halfLifeSeconds, float deltaSeconds)
        {
            if (!MotionMath.Finite(value) || !MotionMath.Finite(velocity) || !MotionMath.Finite(goal) ||
                !MotionMath.Finite(halfLifeSeconds) || halfLifeSeconds < 0 || !MotionMath.Finite(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentException("Spring state, goal and nonnegative times must be finite.");
            if (deltaSeconds == 0) return;
            if (halfLifeSeconds == 0) { value = goal; velocity = 0; return; }
            // Solve (1+k*h)*exp(-k*h)=.5 rather than treating critical damping like a first-order exponential.
            double k = 1.67834699001666 / halfLifeSeconds;
            double offset = value - goal, j = velocity + k * offset, decay = Math.Exp(-k * deltaSeconds);
            value = (float)(goal + (offset + j * deltaSeconds) * decay);
            velocity = (float)((velocity - k * j * deltaSeconds) * decay);
        }
    }
}
