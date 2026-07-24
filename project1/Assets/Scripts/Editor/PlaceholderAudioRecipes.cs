// 에디터 전용 도구(#38). WavSynth와 같은 이유로 파일 전체를 가드한다.
#if UNITY_EDITOR

using System;

namespace Mukseon.Audio.EditorTools
{
    /// <summary>
    /// 임시 클립 한 종류씩의 "레시피"(#38) — 어떤 소리를 어떤 볼륨·피치로 쓸지의 정의.
    ///
    /// 굽는 절차(<see cref="PlaceholderAudioGenerator"/>)와 소리의 내용을 분리해 둔 이유:
    /// 실제 클립을 구해 오면 이 파일만 통째로 지우면 되고, 그때까지 소리를 손볼 때도 여기만 본다.
    /// </summary>
    public static class PlaceholderAudioRecipes
    {
        private const float SfxPeak = 0.85f;

        /// <summary>BGM은 효과음보다 낮게 구워 둔다 — 타격감이 음악에 묻히지 않게.</summary>
        private const float BgmPeak = 0.5f;

        public readonly struct Sfx
        {
            public Sfx(AudioCue cue, string fileName, Func<float[]> synth,
                float volume, float pitchMin, float pitchMax, float minRetrigger)
            {
                Cue = cue;
                FileName = fileName;
                Synth = synth;
                Volume = volume;
                PitchMin = pitchMin;
                PitchMax = pitchMax;
                MinRetrigger = minRetrigger;
            }

            public AudioCue Cue { get; }
            public string FileName { get; }
            public Func<float[]> Synth { get; }
            public float Volume { get; }
            public float PitchMin { get; }
            public float PitchMax { get; }
            public float MinRetrigger { get; }
        }

        public readonly struct Bgm
        {
            public Bgm(BgmTrack track, string fileName, Func<float[]> synth, float volume)
            {
                Track = track;
                FileName = fileName;
                Synth = synth;
                Volume = volume;
            }

            public BgmTrack Track { get; }
            public string FileName { get; }
            public Func<float[]> Synth { get; }
            public float Volume { get; }
        }

        public static readonly Sfx[] SfxAll =
        {
            new Sfx(AudioCue.Swipe, "sfx_swipe", Swipe, 0.50f, 0.94f, 1.08f, 0.03f),
            new Sfx(AudioCue.EnemyHit, "sfx_enemy_hit", EnemyHit, 0.45f, 0.90f, 1.15f, 0.05f),
            new Sfx(AudioCue.EnemyDeath, "sfx_enemy_death", EnemyDeath, 0.60f, 0.92f, 1.08f, 0.06f),
            new Sfx(AudioCue.SoulCollect, "sfx_soul_collect", SoulCollect, 0.35f, 0.96f, 1.20f, 0.04f),
            new Sfx(AudioCue.LevelUp, "sfx_level_up", LevelUp, 0.70f, 1.00f, 1.00f, 0.50f),
            new Sfx(AudioCue.GangshinActivate, "sfx_gangshin", Gangshin, 0.90f, 0.98f, 1.02f, 0.30f),
        };

        public static readonly Bgm[] BgmAll =
        {
            new Bgm(BgmTrack.Lobby, "bgm_lobby", Lobby, 0.70f),
            new Bgm(BgmTrack.Battle, "bgm_battle", Battle, 0.80f),
            new Bgm(BgmTrack.Boss, "bgm_boss", Boss, 0.85f),
        };

        // ---- 효과음 ----------------------------------------------------------------------

        // 대나무를 베는 듯한 스침음: 노이즈의 저역통과 컷오프를 아래에서 위로 훑어 올린다.
        private static float[] Swipe()
        {
            float[] buffer = WavSynth.Buffer(0.18f);
            WavSynth.AddNoise(buffer, new System.Random(11), 1f, 0f, 0.18f, 0.045f, 1200f, 7000f);
            return FinishSfx(buffer, 0.004f);
        }

        // 먹물이 튀는 짧은 타격음: 밝은 노이즈 + 저역 사인의 "툭".
        private static float[] EnemyHit()
        {
            float[] buffer = WavSynth.Buffer(0.14f);
            WavSynth.AddNoise(buffer, new System.Random(22), 1f, 0f, 0.14f, 0.030f, 3500f, 1500f);
            WavSynth.AddSine(buffer, 95f, 0.6f, 0f, 0.14f, 0.050f);
            return FinishSfx(buffer, 0.004f);
        }

        // 처치음은 피격음보다 낮고 길게 — 같은 재료를 어둡게 쓰면 한 계열의 소리로 들린다.
        private static float[] EnemyDeath()
        {
            float[] buffer = WavSynth.Buffer(0.36f);
            WavSynth.AddNoise(buffer, new System.Random(33), 1f, 0f, 0.36f, 0.110f, 1800f, 600f);
            WavSynth.AddSine(buffer, 62f, 0.8f, 0f, 0.36f, 0.160f);
            WavSynth.AddSine(buffer, 41f, 0.5f, 0f, 0.36f, 0.200f);
            return FinishSfx(buffer, 0.006f);
        }

        // 맑은 방울: 배음을 정수배로 쌓고 빠르게 감쇠시킨다.
        private static float[] SoulCollect()
        {
            float[] buffer = WavSynth.Buffer(0.32f);
            WavSynth.AddSine(buffer, 1046.5f, 1.00f, 0f, 0.32f, 0.090f);
            WavSynth.AddSine(buffer, 1568.0f, 0.45f, 0f, 0.32f, 0.070f);
            WavSynth.AddSine(buffer, 2093.0f, 0.20f, 0f, 0.32f, 0.050f);
            return FinishSfx(buffer, 0.004f);
        }

        // 상승 아르페지오(도-미-솔-도). 올라가는 음형이 "좋은 일"로 읽힌다.
        private static float[] LevelUp()
        {
            const float Total = 0.62f;
            float[] buffer = WavSynth.Buffer(Total);
            float[] notes = { 523.25f, 659.26f, 783.99f, 1046.50f };

            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * 0.10f;
                float amplitude = i == notes.Length - 1 ? 1.0f : 0.75f;
                WavSynth.AddSine(buffer, notes[i], amplitude, start, Total - start, 0.140f);
                WavSynth.AddSine(buffer, notes[i] * 2f, amplitude * 0.25f, start, Total - start, 0.090f);
            }

            return FinishSfx(buffer, 0.006f);
        }

        // 징: 서로 정수배가 아닌 부분음을 겹쳐야 금속처럼 들린다(정수배로 쌓으면 오르간 소리가 된다).
        private static float[] Gangshin()
        {
            const float Total = 1.50f;
            float[] buffer = WavSynth.Buffer(Total);
            WavSynth.AddNoise(buffer, new System.Random(44), 0.5f, 0f, 0.30f, 0.050f, 6000f, 2000f);
            WavSynth.AddSine(buffer, 69f, 0.70f, 0f, Total, 0.700f);
            WavSynth.AddSine(buffer, 138f, 1.00f, 0f, Total, 0.550f);
            WavSynth.AddSine(buffer, 207f, 0.60f, 0f, Total, 0.450f);
            WavSynth.AddSine(buffer, 285f, 0.40f, 0f, Total, 0.350f);
            WavSynth.AddSine(buffer, 392f, 0.25f, 0f, Total, 0.250f);
            return FinishSfx(buffer, 0.008f);
        }

        // ---- BGM -------------------------------------------------------------------------
        // 루프 클립에는 FadeEdges를 쓰지 않는다 — 양 끝을 0으로 깎으면 매 바퀴 음량이 꺼졌다 켜진다.
        // 대신 모든 주파수를 루프 길이의 정수배로 스냅해 이음매를 없앤다.

        private static float[] Lobby()
        {
            const float Loop = 6f;
            float[] buffer = WavSynth.Buffer(Loop);
            Drone(buffer, 65.41f, 0.50f, 0.167f, 0.25f, Loop);
            Drone(buffer, 130.81f, 0.35f, 0.333f, 0.35f, Loop);
            Drone(buffer, 196.00f, 0.18f, 0.500f, 0.50f, Loop);
            Drone(buffer, 261.63f, 0.10f, 0.250f, 0.60f, Loop);
            WavSynth.Normalize(buffer, BgmPeak);
            return buffer;
        }

        // 120BPM 4/4 두 마디. 킥(1·3박) + 스네어(2·4박) + 8분음표 하이햇 + 저음 베이스.
        private static float[] Battle()
        {
            const float Loop = 4f;
            float[] buffer = WavSynth.Buffer(Loop);
            var rng = new System.Random(55);

            Drone(buffer, 82.41f, 0.22f, 0.5f, 0.30f, Loop);

            foreach (float beat in new[] { 0f, 2f })
            {
                WavSynth.AddPitchDrop(buffer, 110f, 45f, 0.90f, beat, 0.25f, 0.090f);
            }

            foreach (float beat in new[] { 1f, 3f })
            {
                WavSynth.AddNoise(buffer, rng, 0.50f, beat, 0.18f, 0.050f, 6000f, 3000f);
            }

            for (float t = 0f; t < Loop - 0.01f; t += 0.25f)
            {
                WavSynth.AddNoise(buffer, rng, 0.16f, t, 0.06f, 0.015f, 9000f, 7000f);
            }

            WavSynth.Normalize(buffer, BgmPeak);
            return buffer;
        }

        // 저음 드론 위에 징 두 방. 뒤쪽 징은 루프 끝까지 충분히 감쇠하도록 2.5초에 둔다.
        private static float[] Boss()
        {
            const float Loop = 6f;
            float[] buffer = WavSynth.Buffer(Loop);
            Drone(buffer, 49.00f, 0.55f, 0.167f, 0.20f, Loop);
            Drone(buffer, 73.42f, 0.30f, 0.333f, 0.30f, Loop);
            Drone(buffer, 98.00f, 0.18f, 0.250f, 0.40f, Loop);

            foreach (float hit in new[] { 0f, 2.5f })
            {
                WavSynth.AddSine(buffer, 138f, 0.55f, hit, Loop - hit, 0.900f);
                WavSynth.AddSine(buffer, 207f, 0.30f, hit, Loop - hit, 0.700f);
                WavSynth.AddSine(buffer, 285f, 0.18f, hit, Loop - hit, 0.500f);
            }

            WavSynth.Normalize(buffer, BgmPeak);
            return buffer;
        }

        // 드론은 반드시 루프 격자에 스냅해서 넣는다(주파수도, 음량을 흔드는 LFO도).
        private static void Drone(float[] buffer, float frequency, float amplitude,
            float lfoFrequency, float lfoDepth, float loopSeconds)
        {
            WavSynth.AddDrone(
                buffer,
                WavSynth.SnapToLoop(frequency, loopSeconds),
                amplitude,
                WavSynth.SnapToLoop(lfoFrequency, loopSeconds),
                lfoDepth);
        }

        private static float[] FinishSfx(float[] buffer, float fadeSeconds)
        {
            WavSynth.Normalize(buffer, SfxPeak);
            WavSynth.FadeEdges(buffer, fadeSeconds);
            return buffer;
        }
    }
}

#endif
