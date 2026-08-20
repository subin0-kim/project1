using Mukseon.Core.Persistence;
using UnityEngine;

namespace Mukseon.Core
{
    /// <summary>
    /// <see cref="DirectionColorSettings"/>를 세이브에서 채우는 진입점(#83).
    ///
    /// 설정 서비스 자체는 파일 IO를 모르는 순수 정적 상태로 두고(테스트 용이성), 세이브와 이어 붙이는
    /// 책임만 여기로 뺐다. 유저가 설정 화면을 한 번도 열지 않아도 저장된 값이 적용되어야 하므로
    /// 첫 씬이 뜨기 전에 1회 로드한다.
    /// </summary>
    public static class DirectionColorSettingsBootstrap
    {
        /// <summary>
        /// Domain Reload가 꺼져 있으면 static이 세션 간 유지되므로, 이전 플레이 세션에서 바꾼 설정이
        /// 남지 않도록 진입 시 초기값으로 되돌린다. 곧바로 <see cref="LoadFromSave"/>가 세이브 값을 덮는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            // 구독자를 먼저 끊는다 — 이전 세션의 죽은 핸들러가 남아 있으면 아래 초기화가 그들을 깨운다.
            DirectionColorSettings.ClearSubscribers();
            DirectionColorSettings.ResetToDefaults();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadFromSave()
        {
            DirectionColorSettings.ApplyFrom(SaveGateway.Current);
        }

        /// <summary>현재 설정을 세이브에 기록하고 영속화한다. 설정 UI가 값 변경 직후 호출한다.</summary>
        public static bool Persist()
        {
            DirectionColorSettings.WriteTo(SaveGateway.Current);
            return SaveGateway.Service.Save();
        }
    }
}
