using System.Collections.Generic;
using Mukseon.Core.Persistence;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression.Shrine
{
    /// <summary>
    /// 신당에서 산 영구 업그레이드를 이번 런의 플레이어 스탯에 반영한다(#34).
    /// 플레이어 오브젝트에 붙여 두면 런이 시작될 때 세이브를 읽어 보정을 주입한다.
    ///
    /// <b>Awake가 아니라 Start인 이유:</b> <see cref="PlayerStatSystem.InitializeFromDefinition"/>이
    /// 자신의 Awake에서 런타임 스탯을 전부 비우고 다시 만든다. 같은 오브젝트에 붙은 컴포넌트들의
    /// Awake 순서는 보장되지 않으므로, Awake에서 주입하면 초기화에 지워질 수 있다.
    /// Start는 모든 Awake 이후에 돌기 때문에 순서에 상관없이 안전하다.
    ///
    /// 체력처럼 값을 캐시하는 소비자(<see cref="Combat.PlayerHealth"/>)는
    /// <see cref="PlayerStatSystem.OnStatChanged"/>를 구독하고 있어, 이 주입이 그 Start보다
    /// 앞서든 뒤서든 최종 상태가 같아진다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStatSystem))]
    public class ShrineUpgradeApplier : MonoBehaviour
    {
        [SerializeField, Tooltip("신당 업그레이드 목록. 비우면 보정이 적용되지 않는다.")]
        private ShrineUpgradeCatalog _catalog;

        [SerializeField, Tooltip("비우면 같은 오브젝트에서 찾는다.")]
        private PlayerStatSystem _playerStatSystem;

        [SerializeField, Tooltip("적용된 보정을 로그로 남긴다.")]
        private bool _showDebugLogs;

        // 주입은 런당 1회지만, 버퍼를 필드로 두어 Collect가 매번 리스트를 새로 만들지 않게 한다.
        private readonly List<ShrineStatModifier> _buffer = new List<ShrineStatModifier>();

        private void Awake()
        {
            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
            }
        }

        private void Start()
        {
            if (_catalog == null)
            {
                Debug.LogWarning("[ShrineUpgradeApplier] ShrineUpgradeCatalog가 비어 있어 신당 보정을 적용하지 않습니다.", this);
                return;
            }

            SaveData save = SaveGateway.Current;
            int applied = ShrineUpgradeModifiers.Apply(_catalog, save, _playerStatSystem, _buffer);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[ShrineUpgradeApplier] 신당 보정 {applied}개를 적용했습니다.", this);
            }
#endif
        }
    }
}
