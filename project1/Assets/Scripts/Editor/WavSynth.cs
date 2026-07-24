// 에디터 전용 도구(#38). 임시 사운드를 절차적으로 합성해 .wav로 굽는다.
// Editor 폴더 안이라 플레이어 빌드에는 포함되지 않지만, 이 폴더가 asmdef 하위에 있어
// 규칙이 애매하므로 파일 전체를 명시적으로 가드한다.
#if UNITY_EDITOR

using System;
using System.IO;
using UnityEngine;

namespace Mukseon.Audio.EditorTools
{
    /// <summary>
    /// 임시 오디오 클립 합성용 최소 신디사이저(#38).
    ///
    /// 실제 클립을 구해 오기 전까지 "무슨 일이 일어났는지 귀로 구분되는" 소리만 있으면 되므로,
    /// 사인파·노이즈·지수 감쇠 엔벨로프 세 가지만으로 만든다. 외부 에셋·라이브러리 의존이 없다.
    /// </summary>
    public static class WavSynth
    {
        /// <summary>임시 클립이므로 22.05kHz면 충분하다(파일 크기가 절반).</summary>
        public const int SampleRate = 22050;

        public static float[] Buffer(float seconds)
        {
            return new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate))];
        }

        /// <summary>
        /// 사인파를 지수 감쇠 엔벨로프로 더한다.
        /// </summary>
        /// <param name="decayTau">진폭이 1/e로 줄어드는 시간(초). 작을수록 짧고 타악기 같다.</param>
        public static void AddSine(float[] buffer, float frequency, float amplitude,
            float startSeconds, float durationSeconds, float decayTau)
        {
            int start = Mathf.Clamp(Mathf.RoundToInt(startSeconds * SampleRate), 0, buffer.Length);
            int count = Mathf.RoundToInt(durationSeconds * SampleRate);
            double step = 2.0 * Math.PI * frequency / SampleRate;

            for (int i = 0; i < count && start + i < buffer.Length; i++)
            {
                float t = i / (float)SampleRate;
                buffer[start + i] += amplitude * Envelope(t, decayTau) * (float)Math.Sin(step * i);
            }
        }

        /// <summary>
        /// 시작 주파수에서 끝 주파수로 미끄러지는 사인파(킥 드럼의 "둥" 소리).
        /// 위상을 누적해서 진행해야 주파수가 바뀌는 지점에서 끊기지 않는다.
        /// </summary>
        public static void AddPitchDrop(float[] buffer, float startFrequency, float endFrequency,
            float amplitude, float startSeconds, float durationSeconds, float decayTau)
        {
            int start = Mathf.Clamp(Mathf.RoundToInt(startSeconds * SampleRate), 0, buffer.Length);
            int count = Mathf.RoundToInt(durationSeconds * SampleRate);
            double phase = 0.0;

            for (int i = 0; i < count && start + i < buffer.Length; i++)
            {
                float t = i / (float)SampleRate;
                float progress = count <= 1 ? 1f : i / (float)(count - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);

                phase += 2.0 * Math.PI * frequency / SampleRate;
                buffer[start + i] += amplitude * Envelope(t, decayTau) * (float)Math.Sin(phase);
            }
        }

        /// <summary>
        /// 화이트 노이즈를 1극 저역통과 필터에 통과시켜 더한다. 컷오프를 시작→끝으로 훑으면
        /// 스와이프의 "쉭" 하는 스침음이 된다.
        ///
        /// 1극 저역통과: y[n] = y[n-1] + a·(x[n] − y[n-1]), a = 1 − exp(−2π·fc/fs)
        /// </summary>
        public static void AddNoise(float[] buffer, System.Random rng, float amplitude,
            float startSeconds, float durationSeconds, float decayTau,
            float cutoffStart, float cutoffEnd)
        {
            int start = Mathf.Clamp(Mathf.RoundToInt(startSeconds * SampleRate), 0, buffer.Length);
            int count = Mathf.RoundToInt(durationSeconds * SampleRate);
            float filtered = 0f;

            for (int i = 0; i < count && start + i < buffer.Length; i++)
            {
                float t = i / (float)SampleRate;
                float progress = count <= 1 ? 1f : i / (float)(count - 1);
                float cutoff = Mathf.Lerp(cutoffStart, cutoffEnd, progress);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += a * (white - filtered);

                buffer[start + i] += amplitude * Envelope(t, decayTau) * filtered;
            }
        }

        /// <summary>
        /// 버퍼 전체를 채우는 지속음(드론). 감쇠 엔벨로프도 어택도 없다 —
        /// 루프 BGM의 바탕음은 끝과 시작의 진폭이 같아야 이음매가 들리지 않기 때문이다.
        /// </summary>
        /// <param name="lfoFrequency">음량을 아주 천천히 흔들어 죽은 사인파처럼 들리지 않게 한다.</param>
        /// <param name="lfoDepth">0이면 흔들림 없음, 1이면 0까지 내려간다.</param>
        public static void AddDrone(float[] buffer, float frequency, float amplitude,
            float lfoFrequency, float lfoDepth)
        {
            double step = 2.0 * Math.PI * frequency / SampleRate;
            double lfoStep = 2.0 * Math.PI * lfoFrequency / SampleRate;
            float depth = Mathf.Clamp01(lfoDepth);

            for (int i = 0; i < buffer.Length; i++)
            {
                float lfo = 0.5f * (1f + (float)Math.Sin(lfoStep * i));
                float gain = 1f - depth + depth * lfo;
                buffer[i] += amplitude * gain * (float)Math.Sin(step * i);
            }
        }

        /// <summary>
        /// 루프 클립의 주파수를 루프 길이의 정수배로 스냅한다.
        ///
        /// 루프 지점에서 파형이 딱 떨어지지 않으면 매 바퀴마다 "틱" 하는 잡음이 들린다.
        /// 주기가 루프 길이에 정확히 정수 번 들어가면 끝과 시작의 위상이 이어져 그 잡음이 사라진다.
        /// </summary>
        public static float SnapToLoop(float frequency, float loopSeconds)
        {
            float cycles = Mathf.Max(1f, Mathf.Round(frequency * loopSeconds));
            return cycles / loopSeconds;
        }

        /// <summary>최대 진폭을 지정 피크에 맞춘다. 클립마다 체감 음량을 고르게 하기 위함.</summary>
        public static void Normalize(float[] buffer, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(buffer[i]));
            }

            if (max <= 1e-6f)
            {
                return;
            }

            float scale = peak / max;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= scale;
            }
        }

        /// <summary>시작·끝에 짧은 페이드를 넣어 재생 시작/종료 클릭음을 없앤다(비루프 클립용).</summary>
        public static void FadeEdges(float[] buffer, float fadeSeconds)
        {
            int fade = Mathf.Min(Mathf.RoundToInt(fadeSeconds * SampleRate), buffer.Length / 2);
            for (int i = 0; i < fade; i++)
            {
                float gain = i / (float)fade;
                buffer[i] *= gain;
                buffer[buffer.Length - 1 - i] *= gain;
            }
        }

        /// <summary>16비트 PCM 모노 WAV로 기록한다.</summary>
        public static void WriteWav(string absolutePath, float[] samples)
        {
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            const int bitsPerSample = 16;
            const int channels = 1;
            int dataSize = samples.Length * channels * (bitsPerSample / 8);

            using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });

                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);                                     // fmt 청크 크기
                writer.Write((short)1);                               // 1 = PCM(무압축)
                writer.Write((short)channels);
                writer.Write(SampleRate);
                writer.Write(SampleRate * channels * (bitsPerSample / 8));  // 바이트/초
                writer.Write((short)(channels * (bitsPerSample / 8)));      // 블록 정렬
                writer.Write((short)bitsPerSample);

                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                    writer.Write((short)Mathf.RoundToInt(clamped * short.MaxValue));
                }
            }
        }

        // 2ms 어택으로 시작 클릭을 없애고, 그 뒤로는 지수 감쇠.
        private static float Envelope(float t, float decayTau)
        {
            const float AttackSeconds = 0.002f;
            float attack = AttackSeconds <= 0f ? 1f : Mathf.Min(1f, t / AttackSeconds);
            float decay = decayTau <= 0f ? 1f : Mathf.Exp(-t / decayTau);
            return attack * decay;
        }
    }
}

#endif
