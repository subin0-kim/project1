using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Core.Persistence
{
    /// <summary>
    /// JsonUtility는 Dictionary를 직렬화하지 못하므로, 문자열 키 → 정수 값 매핑을
    /// 직렬화 가능한 엔트리 리스트로 감싼다(#33). 신당 업그레이드 레벨 등 키-값 저장에 사용한다.
    /// </summary>
    [Serializable]
    public class SerializableStringIntMap
    {
        [Serializable]
        private struct Entry
        {
            public string Key;
            public int Value;
        }

        [SerializeField]
        private List<Entry> _entries = new List<Entry>();

        public int Count => _entries != null ? _entries.Count : 0;

        public bool ContainsKey(string key) => IndexOf(key) >= 0;

        public bool TryGetValue(string key, out int value)
        {
            int index = IndexOf(key);
            if (index < 0)
            {
                value = 0;
                return false;
            }

            value = _entries[index].Value;
            return true;
        }

        public int GetValueOrDefault(string key, int fallback = 0)
            => TryGetValue(key, out int value) ? value : fallback;

        /// <summary>키가 있으면 값을 갱신하고, 없으면 새 엔트리를 추가한다.</summary>
        public void Set(string key, int value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            EnsureList();
            int index = IndexOf(key);
            if (index >= 0)
            {
                // 구조체는 값 복사이므로 수정 후 다시 대입한다.
                Entry entry = _entries[index];
                entry.Value = value;
                _entries[index] = entry;
            }
            else
            {
                _entries.Add(new Entry { Key = key, Value = value });
            }
        }

        public bool Remove(string key)
        {
            int index = IndexOf(key);
            if (index < 0)
            {
                return false;
            }

            _entries.RemoveAt(index);
            return true;
        }

        public void Clear() => _entries?.Clear();

        public IEnumerable<string> Keys
        {
            get
            {
                if (_entries == null)
                {
                    yield break;
                }

                for (int i = 0; i < _entries.Count; i++)
                {
                    yield return _entries[i].Key;
                }
            }
        }

        private void EnsureList()
        {
            if (_entries == null)
            {
                _entries = new List<Entry>();
            }
        }

        private int IndexOf(string key)
        {
            if (_entries == null || string.IsNullOrEmpty(key))
            {
                return -1;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Key == key)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
