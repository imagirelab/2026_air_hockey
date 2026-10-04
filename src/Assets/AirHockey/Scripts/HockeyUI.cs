using UnityEngine;
using UnityEngine.UI;

namespace AirHockey
{
    /// <summary>日本語のスコア表示とメッセージ演出。Canvasは実行時に組み立てる。</summary>
    public class HockeyUI : MonoBehaviour
    {
        Text scoreText, messageText, hintText, subText;
        Image flash;
        float msgTimer, msgDuration, msgPop;
        Color flashColor;
        Font font;

        static readonly Color Cream = new Color(1f, 0.94f, 0.82f);
        static readonly Color Amber = new Color(1f, 0.72f, 0.3f);

        void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic" }, 64);
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            flash = new GameObject("Flash", typeof(Image)).GetComponent<Image>();
            flash.transform.SetParent(canvasGo.transform, false);
            Stretch(flash.rectTransform);
            flash.raycastTarget = false;
            flash.color = Color.clear;

            scoreText = MakeText(canvasGo.transform, "Score", 64, new Vector2(0.5f, 1f), new Vector2(0, -70), Cream);
            subText = MakeText(canvasGo.transform, "Sub", 30, new Vector2(0.5f, 1f), new Vector2(0, -135), Amber);
            subText.text = $"{HockeyConfig.WinScore}点先取で勝利";
            messageText = MakeText(canvasGo.transform, "Message", 150, new Vector2(0.5f, 0.5f), new Vector2(0, 120), Cream);
            hintText = MakeText(canvasGo.transform, "Hint", 28, new Vector2(0.5f, 0f), new Vector2(0, 50), new Color(1f, 1f, 1f, 0.7f));
            hintText.text = "マウスでマレットを動かしてパックを打ち返そう";
            UpdateScore();
        }

        void OnEnable()
        {
            GameManager.GoalScored += OnGoal;
            GameManager.MatchEnded += OnEnd;
            GameManager.Countdown += OnCountdown;
            GameManager.MatchStarted += OnStart;
        }

        void OnDisable()
        {
            GameManager.GoalScored -= OnGoal;
            GameManager.MatchEnded -= OnEnd;
            GameManager.Countdown -= OnCountdown;
            GameManager.MatchStarted -= OnStart;
        }

        Text MakeText(Transform parent, string name, int size, Vector2 anchor, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = color;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1600, size * 1.6f);
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0.15f, 0.07f, 0.02f, 0.85f);
            sh.effectDistance = new Vector2(3, -3);
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void UpdateScore()
        {
            var gm = GameManager.Instance;
            int p = gm ? gm.PlayerScore : 0, a = gm ? gm.AIScore : 0;
            scoreText.text = $"あなた  {p}  :  {a}  NPC";
        }

        public void ShowMessage(string msg, Color color, float duration, float pop = 1.6f)
        {
            messageText.text = msg;
            messageText.color = color;
            msgTimer = msgDuration = duration;
            msgPop = pop;
        }

        public void Flash(Color c) => flashColor = c;

        void OnStart()
        {
            UpdateScore();
            hintText.text = "マウスでマレットを動かしてパックを打ち返そう";
            subText.text = $"{HockeyConfig.WinScore}点先取で勝利";
        }

        void OnCountdown(string s)
        {
            bool go = s.EndsWith("！");
            ShowMessage(s, go ? Amber : Cream, go ? 0.9f : 0.7f, go ? 1.8f : 1.4f);
        }

        void OnGoal(bool byPlayer)
        {
            UpdateScore();
            var gm = GameManager.Instance;
            ShowMessage(byPlayer ? "ゴール！" : "失点…", byPlayer ? Amber : new Color(0.7f, 0.85f, 1f), 1.6f, 2.2f + gm.Intensity);
            Flash(byPlayer ? new Color(1f, 0.75f, 0.35f, 0.25f + gm.Intensity * 0.3f) : new Color(0.3f, 0.45f, 0.8f, 0.25f));
            if (gm.MatchPoint && gm.PlayerScore < HockeyConfig.WinScore && gm.AIScore < HockeyConfig.WinScore)
                subText.text = "マッチポイント！";
        }

        void OnEnd(bool playerWon)
        {
            ShowMessage(playerWon ? "あなたの勝ち！" : "NPCの勝ち…", playerWon ? Amber : new Color(0.75f, 0.85f, 1f), 9999f, 2.5f);
            subText.text = playerWon ? "おめでとうございます！" : "もう一度挑戦しよう";
            hintText.text = "クリックでもう一度対戦";
            Flash(playerWon ? new Color(1f, 0.85f, 0.4f, 0.6f) : new Color(0.2f, 0.3f, 0.6f, 0.4f));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (msgTimer > 0f)
            {
                msgTimer -= dt;
                float age = msgDuration - msgTimer;
                float s = Mathf.Lerp(msgPop, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.25f), 3f));
                s *= 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.02f;
                messageText.rectTransform.localScale = Vector3.one * s;
                var c = messageText.color;
                c.a = Mathf.Clamp01(msgTimer / 0.3f);
                messageText.color = c;
            }
            else if (messageText.color.a > 0f)
            {
                var c = messageText.color; c.a = 0f; messageText.color = c;
            }
            if (flashColor.a > 0f)
            {
                flash.color = flashColor;
                flashColor.a = Mathf.MoveTowards(flashColor.a, 0f, dt * 0.8f);
            }
            else flash.color = Color.clear;
        }
    }
}
