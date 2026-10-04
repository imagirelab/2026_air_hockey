using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AirHockey
{
    /// <summary>
    /// 録画用のオフライン音声ミキサー。固定フレームレートで進むゲーム時間に合わせて
    /// 効果音とBGMをバッファへ書き込み、最後にWAVとして保存する。
    /// </summary>
    public static class OfflineMix
    {
        public const int Rate = 44100;
        public static bool Active { get; private set; }
        public static int FrameSamples { get; private set; } = Rate / 30;
        public static float FrameSeconds => FrameSamples / (float)Rate;

        static float[] buffer = new float[0];
        static long cursor;                       // 現在フレームの開始サンプル位置
        static readonly Dictionary<AudioClip, float[]> clipData = new Dictionary<AudioClip, float[]>();

        public static void Register(AudioClip clip, float[] data) => clipData[clip] = data;

        public static void Begin(int fps)
        {
            FrameSamples = Rate / fps;
            Active = true;
            cursor = 0;
            buffer = new float[Rate * 60];
        }

        /// <summary>1フレーム分だけ時間を進める（録画側が毎フレーム呼ぶ）</summary>
        public static void Advance() => cursor += FrameSamples;

        public static long Cursor => cursor;

        /// <summary>現在時刻にクリップを重ねる</summary>
        public static void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (!Active || clip == null || !clipData.TryGetValue(clip, out var src)) return;
            int n = Mathf.FloorToInt(src.Length / pitch);
            Ensure(cursor + n);
            for (int i = 0; i < n; i++)
                buffer[cursor + i] += Sample(src, i * pitch) * volume;
        }

        /// <summary>ループ音源（BGMレイヤー）を現在フレーム分だけ書き込む。戻り値は新しい再生位置。</summary>
        public static double Loop(AudioClip clip, double phase, int samples, float vol0, float vol1, float pitch)
        {
            if (!Active || !clipData.TryGetValue(clip, out var src)) return phase;
            Ensure(cursor + samples);
            for (int i = 0; i < samples; i++)
            {
                float v = Mathf.Lerp(vol0, vol1, i / (float)samples);
                if (v > 0f) buffer[cursor + i] += Sample(src, (float)phase) * v;
                phase += pitch;
                if (phase >= src.Length) phase -= src.Length;
            }
            return phase;
        }

        static float Sample(float[] src, float pos)
        {
            int i = (int)pos;
            if (i >= src.Length - 1) return i < src.Length ? src[i] : 0f;
            float f = pos - i;
            return src[i] + (src[i + 1] - src[i]) * f;
        }

        static void Ensure(long length)
        {
            if (length <= buffer.Length) return;
            System.Array.Resize(ref buffer, (int)Mathf.Max(length, buffer.Length * 2));
        }

        /// <summary>現在位置までを16bitステレオWAVで保存する</summary>
        public static void Save(string path)
        {
            int n = (int)cursor;
            using var w = new BinaryWriter(File.Create(path));
            int dataBytes = n * 4;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)2); w.Write(Rate); w.Write(Rate * 4); w.Write((short)4); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(dataBytes);
            for (int i = 0; i < n; i++)
            {
                // 軽いソフトクリップで音割れを防ぐ
                float s = (float)System.Math.Tanh(buffer[i] * 0.9f);
                short v = (short)(s * 32000f);
                w.Write(v); w.Write(v);
            }
            Active = false;
        }
    }
}
