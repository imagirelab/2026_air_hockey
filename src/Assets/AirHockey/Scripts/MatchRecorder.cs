using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace AirHockey
{
    /// <summary>
    /// 1試合を固定フレームレートで録画する。
    /// 起動引数: -autoplay（AI同士で対戦） -record &lt;出力パス（拡張子なし）&gt; -ffmpeg &lt;ffmpeg.exeのパス&gt; -fps &lt;フレームレート&gt;
    /// 映像はffmpegへ生フレームを送ってmp4にし、音声はOfflineMixでWAVに書き出す。
    /// </summary>
    public class MatchRecorder : MonoBehaviour
    {
        static string outBase, ffmpegPath = "ffmpeg";
        static int fps = 30;

        Process ffmpeg;
        Stream stdin;
        readonly System.Text.StringBuilder rallies = new System.Text.StringBuilder();

        void OnEnable() => GameManager.GoalScored += OnGoal;
        void OnDisable() => GameManager.GoalScored -= OnGoal;
        void OnGoal(bool byPlayer) => rallies.Append($"{GameManager.Instance.RallyTime:F1}s ");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return; // ブラウザ版では録画しない
#endif
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-autoplay": GameManager.AutoPlay = true; break;
                    case "-record": outBase = next; break;
                    case "-ffmpeg": ffmpegPath = next; break;
                    case "-fps": int.TryParse(next, out fps); break;
                }
            }
            if (string.IsNullOrEmpty(outBase)) return;
            GameManager.AutoPlay = true;
            GameManager.AutoRestart = false;
            var go = new GameObject("MatchRecorder");
            DontDestroyOnLoad(go);
            go.AddComponent<MatchRecorder>();
        }

        void Awake()
        {
            Time.captureFramerate = fps;
            OfflineMix.Begin(fps);
        }

        IEnumerator Start()
        {
            yield return null;
            int w = Screen.width & ~1, h = Screen.height & ~1;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outBase)));
            ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {w}x{h} -r {fps} -i - " +
                            $"-vf vflip -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p \"{outBase}_video.mp4\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            });
            stdin = ffmpeg.StandardInput.BaseStream;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var bytes = new byte[w * h * 4];
            var endOfFrame = new WaitForEndOfFrame();
            float afterEnd = 0f;
            int frames = 0;
            while (true)
            {
                yield return endOfFrame;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                tex.GetRawTextureData<byte>().CopyTo(bytes);
                stdin.Write(bytes, 0, bytes.Length);
                OfflineMix.Advance();
                frames++;

                var gm = GameManager.Instance;
                if (gm && gm.Phase == Phase.GameOver) afterEnd += 1f / fps;
                if (afterEnd > 6f || frames > fps * 60 * 15) break; // 終了画面を6秒映したら終わり（安全のため最長15分）
            }

            stdin.Flush();
            stdin.Close();
            ffmpeg.WaitForExit();
            OfflineMix.Save(outBase + "_audio.wav");
            File.WriteAllText(outBase + "_done.txt", $"frames={frames} fps={fps} score={GameManager.Instance.PlayerScore}-{GameManager.Instance.AIScore} rallies={rallies}");
            Application.Quit();
        }
    }
}
