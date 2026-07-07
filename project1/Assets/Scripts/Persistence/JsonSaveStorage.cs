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

        private readonly string _filePath;

        public JsonSaveStorage()
            : this(Path.Combine(Application.persistentDataPath, DefaultFileName))
        {
        }

        /// <summary>저장 경로를 직접 지정한다(테스트에서 임시 경로 주입용).</summary>
        public JsonSaveStorage(string filePath)
        {
            _filePath = filePath;
        }

        public string FilePath => _filePath;

        public bool Exists() => File.Exists(_filePath);

        public SaveData Load()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
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

        public void Save(SaveData data)
        {
            if (data == null)
            {
                return;
            }

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            string tempPath = _filePath + TempSuffix;

            // 임시 파일에 먼저 완전히 기록한 뒤 원자적으로 교체한다.
            File.WriteAllText(tempPath, json);

            if (File.Exists(_filePath))
            {
                // File.Replace는 대상이 존재해야 하며, 교체를 원자적으로 수행한다.
                File.Replace(tempPath, _filePath, null);
            }
            else
            {
                File.Move(tempPath, _filePath);
            }
        }

        public void Delete()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }
}
