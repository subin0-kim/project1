using UnityEngine;

namespace Mukseon.Core.Persistence
{
    /// <summary>
    /// <see cref="SaveService"/>의 런타임 소유자(#36). 세이브 레이어(#33)는 순수 C#으로 완성돼 있었으나
    /// 인스턴스를 들고 있는 곳이 없었고, 캐릭터 선택 화면이 그 첫 소비자다.
    ///
    /// 최초 접근 시 <see cref="JsonSaveStorage"/>로 지연 로드한다. <see cref="Configure"/>는 테스트나
    /// 부트스트랩이 저장소를 갈아끼우기 위한 seam이다 — <see cref="SaveService"/>가 저장소를 주입받도록
    /// 설계된 이점을 파사드에서 버리지 않기 위함이다.
    ///
    /// <b>메인 스레드 전용이다.</b> 기본 저장소가 <c>Application.persistentDataPath</c>를 읽는데
    /// 이 API 자체가 메인 스레드에서만 호출 가능하므로(<see cref="JsonSaveStorage.FilePath"/>),
    /// 지연 초기화에 lock을 걸어도 워커 스레드에서의 첫 접근은 어차피 예외로 실패한다.
    /// lock은 실제로 얻지 못하는 안전성을 보장하는 것처럼 보이게 만들 뿐이므로 두지 않고,
    /// 대신 호출 규약을 여기에 명시한다. 백그라운드 저장이 필요해지면 저장소 계층에서
    /// 경로를 미리 캐시한 뒤 IO만 넘기는 방식으로 풀어야 한다.
    /// </summary>
    public static class SaveGateway
    {
        private static SaveService _service;

        /// <summary>세이브 서비스. 최초 접근 시 저장소에서 로드한다.</summary>
        public static SaveService Service
        {
            get
            {
                if (_service == null)
                {
                    _service = new SaveService(null);
                    _service.Load();
                }

                return _service;
            }
        }

        /// <summary>현재 세이브 데이터(로드 보장).</summary>
        public static SaveData Current => Service.Current;

        /// <summary>저장소를 지정해 서비스를 재구성한다. 최초 접근 전에 호출해야 한다.</summary>
        public static void Configure(ISaveStorage storage)
        {
            _service = new SaveService(storage);
            _service.Load();
        }

        /// <summary>
        /// Domain Reload가 꺼져 있으면 static이 세션 간 유지되므로, 이전 세션의 세이브 인스턴스가
        /// 남지 않도록 진입 시 비운다. 다음 접근에서 다시 로드된다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            _service = null;
        }
    }
}
