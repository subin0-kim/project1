using System;
using System.IO;
using UnityEngine;

namespace Mukseon.Core.Persistence
{
    /// <summary>
    /// JsonUtility 기반 파일 저장소(#33). 기본적으로 Application.persistentDataPath에 JSON으로 저장한다.
    /// 저장 시 임시 파일에 먼저 완전히 기록한 뒤 교체(File.Replace/Move)하여,
    /// 쓰기 도중 중단되더라도 기존 저장 파일이 손상되지 않도록 한다(원자적 쓰기).
    /// </summary>
    public class JsonSaveStorage : ISaveStorage
    {
        private const string DefaultFileName = "save.json";
        private const string TempSuffix = ".tmp";

        // Application.persistentDataPath는 메인 스레드에서만 접근 가능하므로, 생성자에서 즉시 참조하지 않고
        // 실제 IO가 일어나는 시점(FilePath 접근)까지 경로 결정을 지연한다(DI/백그라운드 스레드/정적 초기화 안전).
        private readonly string _customFilePath;
        private string _resolvedFilePath;

        public JsonSaveStorage()
        {
        }

        /// <summary>저장 경로를 직접 지정한다(테스트에서 임시 경로 주입용).</summary>
        public JsonSaveStorage(string filePath)
        {
            _customFilePath = filePath;
        }

        public string FilePath
        {
            get
            {
                if (_resolvedFilePath == null)
                {
                    _resolvedFilePath = _customFilePath ?? Path.Combine(Application.persistentDataPath, DefaultFileName);
                }

                return _resolvedFilePath;
            }
        }

        public bool Exists() => File.Exists(FilePath);

        public SaveData Load()
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[JsonSaveStorage] 저장 파일을 읽지 못했습니다: {exception.Message}");
                return null;
            }
        }

        public bool Save(SaveData data)
        {
            if (data == null)
            {
                return false;
            }

            string tempPath = FilePath + TempSuffix;
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(data, prettyPrint: true);

                // 임시 파일에 먼저 완전히 기록한 뒤 원자적으로 교체한다.
                File.WriteAllText(tempPath, json);

                if (File.Exists(FilePath))
                {
                    // File.Replace는 대상이 존재해야 하며, 교체를 원자적으로 수행한다.
                    File.Replace(tempPath, FilePath, null);
                }
                else
                {
                    File.Move(tempPath, FilePath);
                }

                return true;
            }
            catch (Exception exception)
            {
                // Load()와 대칭적으로 IO 실패를 방어한다. 저장 트리거가 게임플레이 핸들러 한복판에서
                // 호출되더라도(#34/#36/#61/#39) 저장 실패가 예외로 전파되지 않도록 한다.
                Debug.LogError($"[JsonSaveStorage] 저장 파일을 저장하지 못했습니다: {exception.Message}");

                // 실패 시 남은 임시 파일을 정리한다(2차 예외는 무시).
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // 정리 실패는 무시한다.
                    }
                }

                return false;
            }
        }

        public void Delete()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
