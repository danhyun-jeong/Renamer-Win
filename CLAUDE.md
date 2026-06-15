# Renamer Windows — Claude Code 컨텍스트

이 파일은 CLAUDE_WINDOWS.md를 기반으로 생성된 프로젝트입니다.
세부 스펙은 CLAUDE_WINDOWS.md를 참조하세요.

## 프로젝트 구조

```
Renamer.csproj          - .NET 8 WPF 프로젝트
App.xaml / App.xaml.cs  - 앱 진입점, 트레이 아이콘, 서비스 초기화
MainWindow.xaml/.cs     - 메인 팝업 창 (트레이 클릭 시 표시)
SettingsWindow.xaml/.cs - 설정 창
Models/                 - AppSettings, ActivityEntry, StatEntry
Services/               - 핵심 비즈니스 로직
  ClaudeService.cs      - Anthropic API 클라이언트
  FileWatcherService.cs - 다운로드 폴더 감시 + 파일 처리 오케스트레이터
  PdfAnalyzer.cs        - PDF 텍스트 추출 + Claude 분석
  ImageAnalyzer.cs      - 이미지 base64 인코딩 + Claude 비전 분석
  NameTemplate.cs       - 파일명 템플릿 렌더링 + 정제
  LogService.cs         - 활동 로그 (ObservableCollection, JSON 저장)
  StatsService.cs       - 통계 (별도 파일, 60일 보관)
  SettingsService.cs    - 설정 JSON 저장/로드
```

## 빌드 명령

```
dotnet build
dotnet run
dotnet publish -c Release -r win-x64 --self-contained
```

## 주요 의존성

- Hardcodet.NotifyIcon.Wpf 2.0.1 — 시스템 트레이
- PdfPig 0.1.9 — PDF 텍스트 추출
- System.Drawing.Common 8.0.0 — 트레이 아이콘 생성
