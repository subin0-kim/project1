using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    public static class SwipeAttackGeometry
    {
        public static Vector2 ToVector(SwipeDirection direction)
        {
            switch (direction)
            {
                case SwipeDirection.Up:
                    return Vector2.up;
                case SwipeDirection.Down:
                    return Vector2.down;
                case SwipeDirection.Left:
                    return Vector2.left;
                case SwipeDirection.Right:
                    return Vector2.right;
                default:
                    return Vector2.zero;
            }
        }

        public static bool IsTargetWithinArc(
            Vector2 origin,
            Vector2 forwardDirection,
            Vector2 targetPosition,
            float arcAngleDegrees)
        {
            if (forwardDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            Vector2 toTarget = targetPosition - origin;
            if (toTarget.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            float halfAngle = Mathf.Clamp(arcAngleDegrees, 0f, 180f) * 0.5f;
            float dotThreshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);

            Vector2 normalizedForward = forwardDirection.normalized;
            Vector2 normalizedToTarget = toTarget.normalized;

            return Vector2.Dot(normalizedForward, normalizedToTarget) >= dotThreshold;
        }
    }
}
