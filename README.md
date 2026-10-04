# ウッド・エアホッケー

NPCと対戦する、主観視点の3Dエアホッケーゲームです。5点先取で勝利です。
木目のテーブルと暖色の照明で、木のぬくもりを感じる見た目にしています。得点が進むほど、音楽と演出がだんだん派手になります。

**▶ ブラウザで遊ぶ: <https://imagirelab.com/2026_air_hockey/>**

スマートフォンとPCのどちらのブラウザでも遊べます。

## 遊び方

| 端末 | 操作 |
| --- | --- |
| スマートフォン | 画面をなぞる・フリックしてマレットを動かします。指の動いた量だけマレットが動くので、画面のどこをなぞっても構いません |
| PC | マウスでマレットを動かします。Tabキーで、AI同士の自動対戦に切り替えられます |

- 自分の陣地（手前側）の中だけでマレットを動かせます。
- 相手のゴールにパックを入れると1点です。5点を先に取った方が勝ちです。
- 試合が終わったら、クリックまたはタップでもう一度対戦できます。
- iPhoneはマナーモード中だと音が鳴りません（ブラウザの仕様によるものです）。

## 特徴

- **だんだん派手になる演出**: 得点が進むと、BGMにベース・ドラム・ハイハット・メロディが順に加わります。パックの残像、火花、テーブル縁のLED、紙吹雪も増えていきます。マッチポイントでは画面の縁が赤く脈打ち、決勝点はスローモーションになります。
- **NPCのAI**: 守る・パックの裏へ回り込む・攻める、を切り替えます。移動速度と加速度に上限があり、反応の遅れや予測の誤差もあるので、人間らしく失点もします。ラリーが長引くと疲れて動きが鈍り、1点がおよそ10秒で決まるように調整しています。
- **素材はすべて手続き生成**: 木目テクスチャ（メープル、ウォルナット、オーク、杉）、効果音、BGMは、外部の素材を使わずコードで作っています。
- **縦画面に対応**: スマートフォンの縦画面では、テーブル全体が見える視点とUI配置に自動で切り替わります。

## 開発環境

- Unity 6000.5.9f1（Universal Render Pipeline / Input System）
- Unityプロジェクトは `src/` にあります。シーンは `src/Assets/Scenes/AirHockey.unity` です。

### ファイル構成

```text
src/Assets/
├── AirHockey/
│   ├── Scripts/
│   │   ├── GameManager.cs     試合の進行、パックの物理、入力（マウス・フリック）
│   │   ├── AIController.cs    NPCの思考（自動対戦ではプレイヤー側にも使用）
│   │   ├── Mallet.cs / Puck.cs
│   │   ├── CameraRig.cs       主観視点カメラ（縦画面の視点切り替え、揺れ演出）
│   │   ├── HockeyUI.cs        スコア・メッセージ表示
│   │   ├── HockeyAudio.cs     効果音とBGMの合成
│   │   ├── HockeyFX.cs        パーティクル、照明、ポストエフェクトの演出
│   │   ├── MatchRecorder.cs   1試合の録画
│   │   ├── OfflineMix.cs      録画用の音声ミキサー
│   │   └── Editor/
│   │       ├── SceneBuilder.cs   シーンの自動構築
│   │       └── WoodTextures.cs   木目テクスチャの生成
│   ├── Resources/GameFont.ttf    日本語フォント（使用する文字だけのサブセット）
│   ├── Materials/ Textures/      シーン構築で生成したアセット
└── WebGLTemplates/AirHockey/     ブラウザ版のWebページ
```

### シーンの作り直し

メニューの **AirHockey > シーンを構築** を実行すると、テーブル・部屋・照明・カメラ・ポストエフェクトをすべて作り直し、`AirHockey.unity` に保存します。木目テクスチャとマテリアルも再生成されます。

作り直すと、GameManagerの「自動対戦」（`autoPlay`）の設定はオフに戻ります。

### エディターでの自動対戦

GameManagerのインスペクターで「自動対戦」（`autoPlay`）にチェックを入れて再生すると、プレイヤー側もAIが操作します。再生中はTabキーでも切り替えられます。

## ブラウザ版のビルドと公開

1. ビルド対象をWebGLにしてビルドします。Webページは `AirHockey` テンプレートを使い、gzip圧縮（解凍フォールバックあり）で出力する設定になっています。
2. 出力した `index.html` と `Build/` を、`gh-pages` ブランチにpushします。GitHub Pagesはこのブランチを公開しています。

WebGLでは、スマホ向けの画質設定（`Mobile_RPAsset`）が使われます。この設定の描画解像度（Render Scale）を100%未満にすると、FSRによる拡大処理が働きます。FSRはWebGLに対応していないため、画面が真っ黒になります。描画解像度は100%のままにしてください（スマートフォンでの解像度は、Webページ側で上限を設けています）。

## 対戦動画の録画

Windows版をビルドし、次のように起動すると、AI同士で1試合を対戦して録画します。終了すると自動で閉じます。録画には [ffmpeg](https://ffmpeg.org/) が必要です。

```bat
AirHockey.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 ^
  -record <出力先（拡張子なし）> -ffmpeg <ffmpeg.exeのパス> -fps 30
```

録画は固定フレームレートで進むので、PCの性能に関係なくコマ落ちしません。映像は `<出力先>_video.mp4`、音声は `<出力先>_audio.wav` に分けて出力されます。最後にffmpegで1つの動画に合成してください。

```bat
ffmpeg -i <出力先>_video.mp4 -i <出力先>_audio.wav -c:v copy -c:a aac <動画ファイル名>.mp4
```

`-autoplay` だけを付けて起動すると、録画せずにAI同士の対戦を眺められます。

## ライセンス

同梱の日本語フォント [M PLUS Rounded 1c](https://fonts.google.com/specimen/M+PLUS+Rounded+1c)（Copyright 2016 The Rounded M+ Project Authors）は、SIL Open Font License 1.1で配布されています。ライセンス全文は `src/Assets/AirHockey/Resources/GameFont_LICENSE.txt` にあります。
