namespace Mukseon.Core.Persistence
{
    /// <summary>
    /// 세이브 데이터의 영속 저장소 추상화(#33). 파일 IO를 캡슐화하여
    /// 서비스 계층을 순수하게 유지하고, 테스트에서 대체 구현을 주입할 수 있게 한다.
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>저장 파일이 존재하는지 여부.</summary>
        bool Exists();

        /// <summary>저장 데이터를 읽어 반환한다. 파일이 없거나 읽을 수 없으면 null을 반환한다.</summary>
        SaveData Load();

        /// <summary>저장 데이터를 영속화한다(원자적 쓰기).</summary>
        void Save(SaveData data);

        /// <summary>저장 파일을 삭제한다(존재하지 않으면 무시).</summary>
        void Delete();
    }
}
