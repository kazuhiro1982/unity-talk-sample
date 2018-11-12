# Chat Talk Sample for Unity

ユニティちゃんと雑談トークするサンプルです

## 使用するライブラリ類

### Assets

- Unity-chan! Model
- IBM Watson SDK for Unity

### 外部API
- Watson SpeechToText API
- Watson TextToSpeech API
- A3RT Talk API

## プログラムの流れ

1. 標準マイクから音声を受け取り、SpeechToTextでテキスト化する
2. テキストをA3RT TalkAPIに送信し、応答を受け取る
3. TextToSpeechで音声化し、再生する

## サンプルを動かすための事前準備

1. 使用するAssetsをimportする
2. Assets/Scenes/TalkSceneを開く
3. unitychanにアタッチされたTalk Scriptの変数設定に各APIの変数をセット

## 実行手順

1. Playボタンで開始
2. 右上の[音声]ボタンを押すか、キーボードの[s]キーで音声認識が開始します
3. 音声が途切れると音声認識が終了し、文章を解析して雑談が開始します
4. (2)の手順を繰り返します
