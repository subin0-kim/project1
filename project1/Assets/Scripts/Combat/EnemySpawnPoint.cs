using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField]
        private Color _gizmoColor = new Color(1f, 0.6f, 0f, 1f);

        [SerializeField, Min(0.05f)]
        private float _gizmoRadius = 0.25f;

        private void OnDrawGizmos()
        {
            Gizmos.color = _gizmoColor;
            Gizmos.DrawWireSphere(transform.position, _gizmoRadius);
        }
    }
}
