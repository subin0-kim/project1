using System;
using System.Collections.Generic;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class EnemyAttackSequence : MonoBehaviour
    {
        private static readonly SwipeDirection[] DirectionPool =
        {
            SwipeDirection.Up,
            SwipeDirection.Down,
            SwipeDirection.Left,
            SwipeDirection.Right
        };

        [SerializeField]
        private SwipeDirection[] _sequence = new SwipeDirection[0];

        private int _currentIndex;

        public int CurrentIndex => _currentIndex;
        public int SequenceLength => _sequence != null ? _sequence.Length : 0;
        public IReadOnlyList<SwipeDirection> Sequence => _sequence;

        public SwipeDirection CurrentDirection
        {
            get
            {
                if (_sequence == null || _sequence.Length == 0 || _currentIndex >= _sequence.Length)
                {
                    return SwipeDirection.None;
                }

                return _sequence[_currentIndex];
            }
        }

        public event Action<int> OnAdvanced;
        public event Action OnSequenceSet;

        public void SetSequence(SwipeDirection[] sequence)
        {
            _sequence = sequence ?? new SwipeDirection[0];
            _currentIndex = 0;
            OnSequenceSet?.Invoke();
        }

        public void Advance()
        {
            if (_sequence == null || _sequence.Length == 0)
            {
                return;
            }

            _currentIndex = (_currentIndex + 1) % _sequence.Length;
            OnAdvanced?.Invoke(_currentIndex);
        }

        public void ResetSequence()
        {
            _currentIndex = 0;
        }

        public static SwipeDirection[] GenerateRandomSequence(int length)
        {
            length = Mathf.Max(1, length);
            SwipeDirection[] sequence = new SwipeDirection[length];
            for (int i = 0; i < length; i++)
            {
                sequence[i] = DirectionPool[UnityEngine.Random.Range(0, DirectionPool.Length)];
            }

            return sequence;
        }
    }
}
