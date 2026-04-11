using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Mukseon.Core.Pool
{
    /// <summary>
    /// UnityEngine.Pool 기반 범용 게임오브젝트 풀 매니저 (싱글톤).
    /// 프리팹 단위로 풀을 관리하며, Inspector에서 초기/최대 사이즈를 설정할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PoolManager : MonoBehaviour
    {
        [System.Serializable]
        public sealed class PoolPreset
        {
            public GameObject Prefab;
            [Min(1)] public int InitialSize = 5;
            [Min(1)] public int MaxSize = 20;
        }

        private static PoolManager _instance;
        public static PoolManager Instance => _instance;

        [SerializeField]
        private List<PoolPreset> _presets = new List<PoolPreset>();

        // 프리팹 InstanceID → ObjectPool
        private readonly Dictionary<int, ObjectPool<GameObject>> _pools =
            new Dictionary<int, ObjectPool<GameObject>>();

        // 인스턴스 InstanceID → 프리팹 InstanceID (Release 시 프리팹 없이도 반환 가능)
        private readonly Dictionary<int, int> _instanceToPrefabId =
            new Dictionary<int, int>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            PreWarm();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 풀에서 오브젝트를 꺼내 지정 위치/회전으로 배치한다.
        /// 풀이 없으면 자동 생성된다.
        /// </summary>
        /// <summary>
        /// 풀에서 오브젝트를 꺼내 지정 위치/회전으로 배치한다.
        /// 위치 설정 후 SetActive(true)를 호출하므로, OnEnable에서 올바른 위치를 읽을 수 있다.
        /// 풀이 없으면 자동 생성된다.
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            ObjectPool<GameObject> pool = GetOrCreatePool(prefab);
            // actionOnGet을 null로 설정했으므로 pool.Get()은 비활성 상태로 반환된다.
            // 위치를 먼저 잡은 뒤 SetActive(true)로 활성화해야
            // OnEnable에서 transform.position을 읽는 컴포넌트(EnemyMover 등)가 올바른 값을 얻는다.
            GameObject obj = pool.Get();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            return obj;
        }

        /// <summary>
        /// 오브젝트를 풀에 반환한다.
        /// 풀에 등록된 인스턴스가 아니면 Destroy 처리된다.
        /// </summary>
        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            int instId = instance.GetInstanceID();
            if (_instanceToPrefabId.TryGetValue(instId, out int prefabId) &&
                _pools.TryGetValue(prefabId, out ObjectPool<GameObject> pool))
            {
                pool.Release(instance);
            }
            else
            {
                Destroy(instance);
            }
        }

        private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab, int initialSize = 5, int maxSize = 20)
        {
            int prefabId = prefab.GetInstanceID();
            if (_pools.TryGetValue(prefabId, out ObjectPool<GameObject> existing))
            {
                return existing;
            }

            // Inspector에 등록된 프리셋이 있으면 해당 사이즈 사용
            foreach (PoolPreset preset in _presets)
            {
                if (preset.Prefab != null && preset.Prefab.GetInstanceID() == prefabId)
                {
                    initialSize = Mathf.Max(1, preset.InitialSize);
                    maxSize = Mathf.Max(initialSize, preset.MaxSize);
                    break;
                }
            }

            ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    // 생성 즉시 비활성화: Get() 호출자가 위치 설정 후 직접 활성화한다.
                    GameObject obj = Instantiate(prefab);
                    obj.SetActive(false);
                    _instanceToPrefabId[obj.GetInstanceID()] = prefabId;
                    return obj;
                },
                actionOnGet: null,                          // 활성화는 Get() 래퍼에서 위치 설정 후 처리
                actionOnRelease: obj => obj.SetActive(false),
                actionOnDestroy: Destroy,
                collectionCheck: false,
                defaultCapacity: initialSize,
                maxSize: maxSize
            );

            _pools[prefabId] = pool;
            return pool;
        }

        private void PreWarm()
        {
            foreach (PoolPreset preset in _presets)
            {
                if (preset.Prefab == null)
                {
                    continue;
                }

                int size = Mathf.Max(1, preset.InitialSize);
                ObjectPool<GameObject> pool = GetOrCreatePool(preset.Prefab, preset.InitialSize, preset.MaxSize);

                List<GameObject> preWarmed = new List<GameObject>(size);
                for (int i = 0; i < size; i++)
                {
                    preWarmed.Add(pool.Get());
                }

                for (int i = 0; i < preWarmed.Count; i++)
                {
                    pool.Release(preWarmed[i]);
                }
            }
        }
    }
}
