using System;
using System.Collections.Generic;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Core.Persistence
{
    /// <summary>
    /// 유저가 바꾼 방향↔색상 매핑의 직렬화 형태(#83). JsonUtility가 Dictionary를 처리하지 못하므로
    /// <see cref="SerializableStringIntMap"/>과 같은 엔트리 리스트 방식을 쓴다.
    ///
    /// 색은 float 4개가 아니라 <b>RRGGBB 16진 문자열</b>로 담는다:
    /// - 세이브 JSON이 사람이 읽을 수 있게 남고, 부동소수 반올림 오차가 라운드트립에 끼지 않는다.
    /// - 알파는 저장하지 않는다. 방향 색은 항상 불투명하며, 글로우 셰이더는 알파를 표시 방식
    ///   on/off 스위치로 쓰기 때문에(<c>EnemyDirectionColorView</c>) 알파를 저장하면 의미가 충돌한다.
    ///
    /// 키는 <see cref="SwipeDirection"/>의 <c>ToString()</c>이다. enum 번호가 바뀌어도 세이브가
    /// 깨지지 않고, 알 수 없는 키는 로드 시 조용히 무시된다.
    /// </summary>
    [Serializable]
    public class DirectionColorOverrides
    {
        [Serializable]
        private struct Entry
        {
            public string Direction;
            public string ColorHex;
        }

        [SerializeField]
        private List<Entry> _entries = new List<Entry>();

        public int Count => _entries != null ? _entries.Count : 0;

        /// <summary>해당 방향에 유저 지정 색이 있으면 파싱해 반환한다. 잘못된 값은 없는 것으로 취급한다.</summary>
        public bool TryGetColor(SwipeDirection direction, out Color color)
        {
            color = default;

            int index = IndexOf(direction);
            if (index < 0)
            {
                return false;
            }

            // ColorUtility는 '#' 접두사를 요구한다. 저장은 접두사 없이 하므로 여기서 붙인다.
            if (!ColorUtility.TryParseHtmlString("#" + _entries[index].ColorHex, out Color parsed))
            {
                return false;
            }

            parsed.a = 1f;
            color = parsed;
            return true;
        }

        /// <summary>방향에 색을 지정한다. 이미 있으면 덮어쓴다. <see cref="SwipeDirection.None"/>은 무시한다.</summary>
        public void SetColor(SwipeDirection direction, Color color)
        {
            if (direction == SwipeDirection.None)
            {
                return;
            }

            EnsureList();

            var entry = new Entry
            {
                Direction = direction.ToString(),
                ColorHex = ColorUtility.ToHtmlStringRGB(color),
            };

            int index = IndexOf(direction);
            if (index >= 0)
            {
                _entries[index] = entry;
            }
            else
            {
                _entries.Add(entry);
            }
        }

        public bool Remove(SwipeDirection direction)
        {
            int index = IndexOf(direction);
            if (index < 0)
            {
                return false;
            }

            _entries.RemoveAt(index);
            return true;
        }

        public void Clear() => _entries?.Clear();

        private void EnsureList()
        {
            if (_entries == null)
            {
                _entries = new List<Entry>();
            }
        }

        private int IndexOf(SwipeDirection direction)
        {
            if (_entries == null || direction == SwipeDirection.None)
            {
                return -1;
            }

            string key = direction.ToString();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Direction == key)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
