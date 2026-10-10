# EzAcrossControl

**つなぐ。操作する。続ける。** · by ElementZero

[全言語と技術ガイド（ポルトガル語）](../../README.md)

Windows のマウスとキーボードで Android 端末を操作できます。指定した画面の端へポインターを移動すると、操作先を切り替えられます。アプリ間の通信はローカルネットワーク内で行われ、アカウントやクラウド経由の通信は不要です。

## バージョン Windows 1.1.4 / Android 1.1.6 を入手

- [Windows インストーラー](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-setup.exe)
- [Android APK](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.6/EZAcrossControl-1.1.6-android.apk)
- [Windows ポータブル ZIP](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-portable.zip)
- [リリースと整合性確認](https://github.com/eddesignerez/EzAcrossControl/releases)

## 必要条件と初回接続

64 ビット版 Windows 10/11、Android 7 以降、および両端末の同一 LAN への接続が必要です。ネイティブ操作に USB を選んだ場合も LAN が必要です。USB デバッグを許可するか、ワイヤレスデバッグをペアリングしてください（Android 11 以降）。ネイティブ操作は端末の UHID 対応状況に依存します。

1. Windows に EzAcrossControl をインストールして起動します。インストーラーはローカルサーバーとプライベートネットワークのファイアウォールを設定するため、管理者権限を求めます。
2. APK をインストールします。Android の **詳細モード → デバッグ → 設定を開く** から開発者向けオプションと使用するデバッグ方式を有効にし、PC を許可します。
3. ワイヤレスデバッグを使う場合は、[詳細ガイド](../../README.md#primeiro-uso)に従い、同梱の ADB でペアリングします。ペアリング用と接続用のポートは異なります。
4. APK で **Auto**、**Wi-Fi**、**USB** のいずれかを選びます。Windows に表示されたホスト IP と、両アプリで同じポート（初期値 **8765**）を入力します。**接続**をタップし、Windows で Android 側の画面端を選択します。

Android は通常システム言語に従います。変更する場合は **詳細モード → 言語**、Windows では診断画面の言語選択を使用します。

## ビジュアル案と検証範囲

![Day の静的なブランド案](../../Brand/Assets/PNG/splash-day-1600x900.png)
![Night の静的なブランド案](../../Brand/Assets/PNG/splash-night-1600x900.png)

これらは**静的なブランド案であり、実行中のアプリのスクリーンショットではありません**。書体と配色の調整案は未承認です。実機での検証は HiPadPlus と LAVIE T11 に限られ、他機種では個別の確認が必要です。Windows インストーラーには Authenticode 署名がありません。[トラブルシューティングと構成](../../README.md#solução-de-problemas)、[サードパーティー表記](../../THIRD-PARTY-NOTICES.md)も参照してください。
