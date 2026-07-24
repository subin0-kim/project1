using System;
using System.Reflection;
using Mukseon.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 오디오 라이브러리 계약 검증(#38).
    ///
    /// 가장 중요한 건 "열거형에 값을 추가했는데 에셋에 클립을 안 넣는" 실수를 잡는 것이다.
    /// 그 경우 코드는 멀쩡히 컴파일되고 게임도 안 죽지만, 그 소리만 조용히 안 난다 —
    /// 플레이해 보기 전엔 아무도 모른다.
    /// </summary>
    public class AudioLibraryTests
    {
        private const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ShippedLibrary_IsLoadableFromResources()
        {
            Assert.That(AudioLibrary.Load(), Is.Not.Null,
                $"Resources/{AudioLibrary.ResourcePath} 에셋이 없으면 게임 전체가 무음이 된다.");
        }

        [Test]
        public void ShippedLibrary_HasClipForEveryCue()
        {
            AudioLibrary library = AudioLibrary.Load();
            Assert.That(library, Is.Not.Null);

            foreach (AudioCue cue in (AudioCue[])Enum.GetValues(typeof(AudioCue)))
            {
                if (cue == AudioCue.None)
                {
                    continue;
                }

                Assert.That(library.HasClip(cue), Is.True, $"효과음 {cue}에 클립이 배선되지 않았습니다.");
            }
        }

        [Test]
        public void ShippedLibrary_HasClipForEveryTrack()
        {
            AudioLibrary library = AudioLibrary.Load();
            Assert.That(library, Is.Not.Null);

            foreach (BgmTrack track in (BgmTrack[])Enum.GetValues(typeof(BgmTrack)))
            {
                if (track == BgmTrack.None)
                {
                    continue;
                }

                Assert.That(library.HasClip(track), Is.True, $"BGM {track}에 클립이 배선되지 않았습니다.");
            }
        }

        [Test]
        public void FindCue_None_ReturnsNull()
        {
            AudioLibrary library = AudioLibrary.Load();
            Assert.That(library, Is.Not.Null);

            Assert.That(library.FindCue(AudioCue.None), Is.Null);
            Assert.That(library.FindTrack(BgmTrack.None), Is.Null);
        }

        // 빈 라이브러리도 예외 없이 null을 돌려줘야 한다 — 에셋을 찾지 못한 상황에서 게임이 죽으면 안 된다.
        [Test]
        public void EmptyLibrary_ReturnsNullInsteadOfThrowing()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            try
            {
                Assert.That(library.FindCue(AudioCue.Swipe), Is.Null);
                Assert.That(library.FindTrack(BgmTrack.Battle), Is.Null);
                Assert.That(library.HasClip(AudioCue.Swipe), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        // 인스펙터에서 min/max를 거꾸로 넣어도 Random.Range가 빈 구간을 받지 않도록 정렬해서 읽는다.
        [Test]
        public void CueDefinition_PitchRange_IsOrderedEvenWhenInverted()
        {
            var definition = new AudioCueDefinition();
            SetField(definition, "_pitchMin", 1.4f);
            SetField(definition, "_pitchMax", 0.8f);

            Assert.That(definition.PitchMin, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(definition.PitchMax, Is.EqualTo(1.4f).Within(1e-5f));
        }

        [Test]
        public void CueDefinition_NegativeRetrigger_ReadsAsZero()
        {
            var definition = new AudioCueDefinition();
            SetField(definition, "_minRetriggerSeconds", -1f);

            Assert.That(definition.MinRetriggerSeconds, Is.EqualTo(0f));
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, NonPublicInstance);
            Assert.That(field, Is.Not.Null, $"필드 {name}를 찾지 못했습니다.");
            field.SetValue(target, value);
        }
    }
}
