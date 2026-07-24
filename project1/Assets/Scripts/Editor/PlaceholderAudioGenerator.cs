// 에디터 전용 도구(#38). WavSynth와 같은 이유로 파일 전체를 가드한다.
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mukseon.Audio.EditorTools
{
    /// <summary>
    /// 임시 오디오 클립을 굽고 <see cref="AudioLibrary"/> 에셋에 배선하는 도구(#38).
    /// 메뉴: Tools / Mukseon / Generate Placeholder Audio
    ///
    /// 소리의 내용은 <see cref="PlaceholderAudioRecipes"/>에 있다. 여기는 "굽고 배선한다"만 한다.
    ///
    /// 실제 클립을 구해 오면 라이브러리 에셋의 Clip 슬롯만 바꾸면 되고, 그 뒤에 이 도구를 다시
    /// 돌려도 <b>플레이스홀더 폴더 밖의 클립이 배선된 항목은 건드리지 않는다</b>.
    /// </summary>
    public static class PlaceholderAudioGenerator
    {
        private const string PlaceholderFolder = "Assets/Audio/Placeholders";
        private const string LibraryFolder = "Assets/Resources/Audio";
        private const string LibraryPath = LibraryFolder + "/AudioLibrary.asset";

        [MenuItem("Tools/Mukseon/Generate Placeholder Audio")]
        public static void GenerateMenu()
        {
            Debug.Log(Generate());
        }

        /// <summary>클립을 굽고 라이브러리를 배선한 뒤 결과 보고서를 돌려준다.</summary>
        public static string Generate()
        {
            var report = new StringBuilder();

            EnsureFolder(PlaceholderFolder);
            EnsureFolder(LibraryFolder);

            foreach (PlaceholderAudioRecipes.Sfx recipe in PlaceholderAudioRecipes.SfxAll)
            {
                Bake(recipe.FileName, recipe.Synth(), report);
            }

            foreach (PlaceholderAudioRecipes.Bgm recipe in PlaceholderAudioRecipes.BgmAll)
            {
                Bake(recipe.FileName, recipe.Synth(), report);
            }

            // 구운 .wav를 AudioClip으로 불러오려면 먼저 임포트가 끝나야 한다.
            AssetDatabase.Refresh();

            AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
                report.AppendLine($"라이브러리 생성: {LibraryPath}");
            }

            WireLibrary(library, report);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine("완료.");
            return report.ToString();
        }

        private static void Bake(string fileName, float[] samples, StringBuilder report)
        {
            // Application.dataPath는 .../Assets 이므로 한 단계 위가 프로젝트 루트다.
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string relative = $"{PlaceholderFolder}/{fileName}.wav";
            WavSynth.WriteWav(Path.Combine(projectRoot, relative), samples);
            report.AppendLine($"  구움: {relative} ({samples.Length / (float)WavSynth.SampleRate:0.00}s)");
        }

        private static void WireLibrary(AudioLibrary library, StringBuilder report)
        {
            var cues = new List<AudioCueDefinition>();
            foreach (PlaceholderAudioRecipes.Sfx recipe in PlaceholderAudioRecipes.SfxAll)
            {
                AudioCueDefinition existing = library.FindCue(recipe.Cue);
                if (existing != null && IsCustomClip(existing.Clip))
                {
                    cues.Add(existing);
                    report.AppendLine($"  유지(실 클립 배선됨): {recipe.Cue}");
                    continue;
                }

                var definition = new AudioCueDefinition();
                SetField(definition, "_cue", recipe.Cue);
                SetField(definition, "_clip", LoadClip(recipe.FileName));
                SetField(definition, "_volume", recipe.Volume);
                SetField(definition, "_pitchMin", recipe.PitchMin);
                SetField(definition, "_pitchMax", recipe.PitchMax);
                SetField(definition, "_minRetriggerSeconds", recipe.MinRetrigger);
                cues.Add(definition);
            }

            var tracks = new List<BgmTrackDefinition>();
            foreach (PlaceholderAudioRecipes.Bgm recipe in PlaceholderAudioRecipes.BgmAll)
            {
                BgmTrackDefinition existing = library.FindTrack(recipe.Track);
                if (existing != null && IsCustomClip(existing.Clip))
                {
                    tracks.Add(existing);
                    report.AppendLine($"  유지(실 클립 배선됨): {recipe.Track}");
                    continue;
                }

                var definition = new BgmTrackDefinition();
                SetField(definition, "_track", recipe.Track);
                SetField(definition, "_clip", LoadClip(recipe.FileName));
                SetField(definition, "_volume", recipe.Volume);
                tracks.Add(definition);
            }

            SetField(library, "_cues", cues);
            SetField(library, "_tracks", tracks);
            library.InvalidateLookup();
            report.AppendLine($"배선: 효과음 {cues.Count}종 / BGM {tracks.Count}종");
        }

        // 플레이스홀더 폴더 밖의 클립 = 사람이 직접 넣은 실제 클립. 도구가 덮어쓰면 안 된다.
        private static bool IsCustomClip(AudioClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(clip);
            return !string.IsNullOrEmpty(path) && !path.StartsWith(PlaceholderFolder, StringComparison.Ordinal);
        }

        private static AudioClip LoadClip(string fileName)
        {
            string path = $"{PlaceholderFolder}/{fileName}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError($"[PlaceholderAudioGenerator] 클립을 불러오지 못했습니다: {path}");
            }

            return clip;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        // 정의 클래스의 필드는 [SerializeField] private이라 리플렉션으로 채운다.
        // SerializedProperty를 쓰지 않는 이유: 열거형 필드를 인덱스로 다뤄야 해서 값이 어긋날 여지가 있다.
        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException($"필드 {name}를 {target.GetType().Name}에서 찾지 못했습니다.");
            }

            field.SetValue(target, value);
        }
    }
}

#endif
