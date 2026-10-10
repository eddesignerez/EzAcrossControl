# EzAcrossControl

**연결하고. 제어하고. 이어가세요.** · by ElementZero

[모든 언어 및 전체 기술 안내서(포르투갈어)](../../README.md)

Windows 마우스와 키보드로 Android 기기를 제어할 수 있습니다. 포인터를 선택한 화면 가장자리로 옮기면 제어 대상을 전환할 수 있습니다. 두 앱은 계정이나 클라우드 경유 없이 로컬 네트워크에서 통신합니다.

## 버전 Windows 1.1.4 / Android 1.1.5 다운로드

- [Windows 설치 프로그램](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-setup.exe)
- [Android APK](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.5/EZAcrossControl-1.1.5-android.apk)
- [Windows 휴대용 ZIP](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.4/EZAcrossControl-1.1.4-windows-x64-portable.zip)
- [릴리스 및 무결성 확인](https://github.com/eddesignerez/EzAcrossControl/releases)

## 요구 사항 및 첫 연결

64비트 Windows 10/11, Android 7 이상, 두 기기의 동일한 LAN 연결이 필요합니다. 기본 제어에 USB를 사용해도 LAN은 필요합니다. USB 디버깅을 허용하거나 무선 디버깅을 페어링하세요(Android 11 이상). 기본 제어 가능 여부는 기기의 UHID 지원에 따라 달라집니다.

1. Windows에 EzAcrossControl을 설치하고 실행합니다. 설치 프로그램은 로컬 서버와 사설 네트워크 방화벽 설정을 위해 관리자 권한을 요청합니다.
2. APK를 설치합니다. Android의 **고급 모드 → 디버깅 → 설정 열기**에서 개발자 옵션과 사용할 디버깅 방식을 활성화한 뒤 컴퓨터를 허용합니다.
3. 무선 디버깅을 사용하려면 [상세 안내](../../README.md#primeiro-uso)에 따라 함께 제공되는 ADB로 기기를 페어링합니다. 페어링 포트와 연결 포트는 다릅니다.
4. APK에서 **Auto**, **Wi-Fi**, **USB** 중 하나를 선택합니다. Windows에 표시된 호스트 IP와 두 앱에 동일한 포트(기본값 **8765**)를 입력합니다. **연결**을 누르고 Windows에서 Android 화면 가장자리를 선택합니다.

Android는 기본적으로 시스템 언어를 따릅니다. **고급 모드 → 언어**에서 변경할 수 있으며, Windows 언어 선택은 진단 화면에 있습니다.

## 시각 자료와 검증 범위

![정적 Day 브랜드 제안](../../Brand/Assets/PNG/splash-day-1600x900.png)
![정적 Night 브랜드 제안](../../Brand/Assets/PNG/splash-night-1600x900.png)

이 이미지는 **정적 브랜드 제안으로, 실행 중인 앱의 스크린샷이 아닙니다**. 글꼴과 색상 조정안은 아직 승인되지 않았습니다. 실제 기기 검증은 HiPadPlus와 LAVIE T11에서만 진행되었으며 다른 기기는 별도 확인이 필요합니다. Windows 설치 프로그램에는 Authenticode 서명이 없습니다. [문제 해결 및 구조](../../README.md#solução-de-problemas)와 [제3자 고지](../../THIRD-PARTY-NOTICES.md)를 참고하세요.
