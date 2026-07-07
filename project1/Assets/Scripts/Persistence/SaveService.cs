using System;

namespace Mukseon.Core.Persistence
{
    /// <summary>
    /// 세이브 데이터의 로드/저장 및 현재 인스턴스 보관을 담당하는 서비스(#33).
    /// 순수 C# 클래스로, 저장소(ISaveStorage)를 주입받아 테스트 가능하게 한다.
    /// 실제 데이터 변경(재화 적립/해금/업그레이드)은 각 소유 이슈의 시스템이 Current를 수정한 뒤 Save()를 호출한다.
    /// </summary>
    public class SaveService
    {
        private readonly ISaveStorage _storage;

        /// <summary>현재 메모리에 로드된 세이브 데이터. Load() 호출 전에는 null일 수 있다.</summary>
        public SaveData Current { get; private set; }

        /// <summary>Current가 (재)로드되거나 저장될 때 발행된다. UI 갱신 구독용.</summary>
        public event Action<SaveData> OnChanged;

        public SaveService(ISaveStorage storage)
        {
            _storage = storage ?? new JsonSaveStorage();
        }

        /// <summary>
        /// 저장소에서 데이터를 로드한다. 파일이 없으면 기본값을,
        /// 있으면 마이그레이션된 데이터를 Current로 설정한다.
        /// </summary>
        public SaveData Load()
        {
            SaveData loaded = _storage.Load();
            Current = loaded == null ? SaveData.CreateDefault() : SaveMigration.Migrate(loaded);
            OnChanged?.Invoke(Current);
            return Current;
        }

        /// <summary>Current를 저장소에 영속화하고 OnChanged를 발행한다.</summary>
        public void Save()
        {
            if (Current == null)
            {
                Current = SaveData.CreateDefault();
            }

            _storage.Save(Current);
            OnChanged?.Invoke(Current);
        }
    }
}
