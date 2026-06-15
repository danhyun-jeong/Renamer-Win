# Renamer — Windows 버전 개발 컨텍스트

## 이 파일의 용도

macOS 앱 "Renamer"의 **Windows 버전**을 개발하기 위한 Claude Code 컨텍스트 문서입니다.
Windows 프로젝트 루트에 `CLAUDE.md`로 저장하면 Claude Code가 자동으로 읽어 맥락을 파악합니다.

---

## 프로젝트 개요

**Renamer**는 사용자의 다운로드 폴더를 감시하다가 새 파일이 추가되면
Claude AI를 통해 내용을 분석하고 파일 이름을 자동으로 바꿔주는 앱입니다.

| 파일 종류 | 판별 기준 | 추출 정보 | 이름 변경 예시 |
|-----------|-----------|-----------|----------------|
| PDF | 학술지 논문 여부 | 저자, 제목, 발행연도 | `KCI_FI003340714.pdf` → `정재훈(2026), 검열, 프로파간다, 영화적 다중첩자.pdf` |
| 이미지 (JPG/PNG 등) | 학술 행사 포스터 여부 | 행사 제목, 날짜 | `1769761623137.jpeg` → `(포스터)상허학회 2026년 겨울 정기학술대회(260220).jpeg` |

macOS 버전은 Swift + SwiftUI 메뉴바 앱으로 완성된 상태입니다.
이 프로젝트는 동일한 기능의 **Windows 버전**입니다.

---

## 기술 스택

- **언어**: C# (.NET 8)
- **UI**: WPF (Windows Presentation Foundation)
- **시스템 트레이**: `Hardcodet.NotifyIcon.Wpf` (NuGet)
- **PDF 텍스트 추출**: `PdfPig` (NuGet)
- **HTTP**: `System.Net.Http.HttpClient` (내장)
- **JSON**: `System.Text.Json` (내장)
- **파일 감시**: `System.IO.FileSystemWatcher` (내장)
- **설정 저장**: JSON 파일 (`%APPDATA%\Renamer\`)

---

## Claude API 연동

### 엔드포인트
```
POST https://api.anthropic.com/v1/messages
```

### 필수 헤더
```
Content-Type: application/json
x-api-key: {USER_API_KEY}
anthropic-version: 2023-06-01
```

### 요청 바디 기본 구조
```json
{
  "model": "claude-haiku-4-5-20251001",
  "max_tokens": 1024,
  "system": "...",
  "messages": [
    { "role": "user", "content": [...] }
  ]
}
```

### 응답 구조 (토큰 사용량 포함)
```json
{
  "content": [{ "type": "text", "text": "..." }],
  "usage": {
    "input_tokens": 850,
    "output_tokens": 60
  }
}
```

### 지원 모델 및 비용
| 모델 ID | Input | Output |
|---------|-------|--------|
| `claude-haiku-4-5-20251001` | $1.00/1M tokens | $5.00/1M tokens |
| `claude-sonnet-4-6` | $3.00/1M tokens | $15.00/1M tokens |

비용 계산:
```
cost = inputTokens / 1_000_000.0 * inputRate + outputTokens / 1_000_000.0 * outputRate
```

---

## PDF 분석 로직

### 텍스트 추출 전략
1. **첫 페이지** 전체 텍스트 (최대 1000자)
2. **2~3페이지** 헤더/푸터 (각 앞 80자 + 뒤 80자, 저널명·권호·연도 포함)
3. **마지막 3페이지**에서 날짜 키워드 포함 줄만 추출

### 스캔 PDF OCR 폴백
첫 페이지에서 추출한 텍스트가 공백만 있거나 비어있으면 스캔 PDF로 판단.
이 경우 OCR로 텍스트를 추출한 뒤 이후 로직을 동일하게 진행.

Windows 구현 방법 (택 1):
- `Windows.Media.Ocr` (WinRT API, 별도 패키지 없이 사용 가능, 한국어 지원)
- `Tesseract.NET` NuGet 패키지 (오픈소스, 언어팩 별도 설치 필요)

OCR 시 처리 순서:
1. PdfPig으로 페이지를 이미지로 렌더링 (2x 해상도 권장)
2. 렌더링된 이미지를 OCR 엔진에 전달
3. 인식된 텍스트를 이후 분석에 사용

OCR 결과도 비어있으면 해당 PDF는 분석 불가로 처리 (조용히 종료).

### 사전 필터 (API 호출 없이 즉시 종료)
파일명 + 첫 페이지 앞 30자에 아래 키워드가 있으면 논문 아님으로 처리:
```
["발제", "발제문", "발표", "발표문", "초고"]
```

### 날짜 줄 추출 키워드 (마지막 페이지)
- **한국어**: `["접수", "수정", "게재", "심사", "투고", "발행", "출판"]` → 해당 줄 ±1줄 포함
- **영어**: `["received", "accepted", "published", "online", "revised", "submitted"]` → 해당 줄만
- 키워드 없으면 마지막 200자 fallback

### System Prompt
```
You are a file naming assistant for academic papers. Respond ONLY with valid JSON. No explanation or markdown.
```

### User Prompt (변수 채워서 사용)
```
[SOURCE 1 — First page (title, authors, journal metadata)]
---
{firstPageSnippet}
---

[SOURCE 2 — Pages 2–3 header/footer excerpts (running head, journal name, volume/year)]
---
{headerFooterText}
---

[SOURCE 3 — Last page (submission / acceptance / publication dates)]
---
{lastPageSnippet}
---

Is this a journal article (학술 논문)?

A journal article typically has: title, author(s), abstract, journal name, DOI, volume/issue number.
Not an article: textbook chapter, thesis, conference poster, report, presentation slides, 발제문, 발표문, 초고, class handout.

For pub_year: look independently in all three sources above.
- SOURCE 1: journal metadata at the top of the first page (volume, issue, year / DOI / copyright line)
- SOURCE 2: running headers or footers (e.g. "Korean J. Edu. 2023, 40(2)")
- SOURCE 3: phrases like "게재 확정일", "최종 게재일", "Accepted", "Published" followed by a date
If two or more sources agree on a 4-digit year, return that year.
If only one source has a year and the others are unavailable, return that year.
If sources conflict, return the year from SOURCE 3 (publication/acceptance date is most authoritative).
If no year is found anywhere, return "".

Respond with JSON only (no markdown, no explanation):
{
  "is_journal_article": true,
  "author": "Use the language of the paper's main body. Korean paper → Korean author names: (1) 1명: 성명 전체 (예: '홍길동'). (2) 2명: '저자1·저자2' (예: '홍길동·김철수'). (3) 3명 이상: 반드시 첫 번째 저자 이름만 쓰고 ' 외' 추가 (예: 저자가 홍길동·김철수·이영희이면 → '홍길동 외'). English paper → (1) 1 author: full name. (2) 2 authors: 'Name1 & Name2'. (3) 3+ authors: first author name only followed by ' et al.' (e.g. 'Smith et al.'). If both Korean and English names appear, use the Korean names.",
  "main_title": "Use the language of the paper's main body. If both Korean and English titles appear (e.g. Korean body + English abstract at the end), use the KOREAN title. Main title ONLY — omit subtitles after ':', '—', or similar separators. If the Korean title has missing word spacing (words concatenated without spaces, which can happen in both scanned and older digital PDFs), restore proper Korean word spacing.",
  "pub_year": "2023"
}

If NOT a journal article:
{"is_journal_article": false, "author": "", "main_title": "", "pub_year": ""}
```

### 응답 파싱
응답 문자열에서 `{` ~ `}` 범위를 추출한 뒤 JSON 파싱.
`is_journal_article == true` 이고 `author`, `main_title`이 모두 비어있지 않아야 유효.

---

## 이미지 분석 로직

### 조건
- 지원 확장자: jpg, jpeg, png, gif, webp, heic, heif
- 20MB 초과 파일은 분석 건너뜀

### 이미지 전송 방식
파일을 base64로 인코딩하여 Claude의 vision 기능으로 전달.

```json
{
  "model": "...",
  "max_tokens": 1024,
  "system": "...",
  "messages": [{
    "role": "user",
    "content": [
      {
        "type": "image",
        "source": {
          "type": "base64",
          "media_type": "image/jpeg",
          "data": "{BASE64_ENCODED_IMAGE}"
        }
      },
      {
        "type": "text",
        "text": "{USER_PROMPT}"
      }
    ]
  }]
}
```

media_type 매핑: `jpg/jpeg` → `image/jpeg`, `png` → `image/png`, `gif` → `image/gif`, `webp` → `image/webp`

### System Prompt
```
You are a file naming assistant. Analyze images and respond ONLY with valid JSON. No explanation or markdown.
```

### User Prompt
```
Is this image an event poster (행사 포스터, 공연 포스터, 전시 포스터, 강연 포스터, 세미나 포스터, etc.)?

A poster typically advertises an event with: event title, date/time, venue, and promotional design.
Not a poster: regular photos, screenshots, product images, logos, documents, memes.

Respond with JSON only (no markdown, no explanation):
{
  "is_poster": true,
  "event_title": "exact event title as shown in the image (preserve original language)",
  "year":  "4-digit year, e.g. 2025. Empty string if not found.",
  "month": "2-digit month with leading zero, e.g. 06. Empty string if not found.",
  "day":   "2-digit day with leading zero, e.g. 15. Empty string if not found."
}

If NOT a poster:
{"is_poster": false, "event_title": "", "year": "", "month": "", "day": ""}
```

---

## 파일 이름 규칙

### 기본 템플릿
- 논문 PDF: `{name}({year}), {title}`
- 포스터 이미지: `(포스터){title}({when: YYMMDD})`

### 변수
| 변수 | 대상 | 설명 |
|------|------|------|
| `{name}` | 논문 | 저자 이름 |
| `{year}` | 논문 | 발행연도 (4자리) |
| `{title}` | 논문/포스터 | 제목 |
| `{when: FORMAT}` | 포스터 | 날짜 (FORMAT 지정) |

### 날짜 포맷 토큰 (`{when: FORMAT}` 내부)
파싱은 앞에서부터 가장 긴 토큰 우선 (YYYY > YY, MM > M, DD > D):

| 토큰 | 출력 |
|------|------|
| `YYYY` | 4자리 연도 (예: 2026) |
| `YY` | 2자리 연도 (예: 26) |
| `MM` | 2자리 월 선행 0 포함 (예: 06) |
| `M` | 월 선행 0 없음 (예: 6) |
| `DD` | 2자리 일 선행 0 포함 (예: 07) |
| `D` | 일 선행 0 없음 (예: 7) |
| 그 외 | 그대로 출력 (`.`, `/`, `-` 등) |

### 파일명 정제 규칙
1. 금지 문자 (`/`, `:`, `\0`) → `-` 로 대체
2. `<` → `〈`, `>` → `〉`
3. 빈 값 치환 후 남은 빈 괄호 쌍 `()`, `[]` 제거 (반복 적용, 중첩 처리)
4. 앞뒤 공백 제거

### 중복 파일명 처리
대상 경로에 파일이 이미 존재하면 `파일명 (2).ext`, `파일명 (3).ext` 순으로 번호 증가.

---

## 재시도 로직

API 오류 발생 시 최대 3회 자동 재시도. 대기 시간: 5초 → 15초 → 30초.
재시도 전 파일이 여전히 존재하는지 확인 후 진행.

---

## 활동 로그

### 로그 메시지 형식
```
"이미지 분석 중: {파일명}"
"PDF 분석 중: {파일명}"
"포스터 아님, 제목 변경 안 함: {파일명}"
"논문 아님, 제목 변경 안 함: {파일명}"
"✓ {원본파일명}\n  → {변경된파일명}"
"↩ 재시도 {n}/{max} ({delay}초 후): {파일명}"
"⚠️ 최종 실패 ({파일명}): {오류메시지}"
"모니터링 시작: 다운로드 폴더"
"모니터링 중지"
```

### ActivityEntry 필드
```csharp
Guid Id
DateTime Timestamp
string Message
int? InputTokens   // null이면 API 미호출 항목
int? OutputTokens
string? Model
double? Cost
```

### 저장
- 파일: `%APPDATA%\Renamer\activity_log.json`
- 최대 보관 건수 초과 시 오래된 것부터 삭제
- 로그 초기화 시 통계에는 영향 없음

---

## 통계

로그와 **완전히 독립**된 별도 파일에 저장. 로그 삭제 시에도 통계 유지.

### StatEntry 필드
```csharp
DateTime Timestamp
bool WasRenamed
double Cost
```

### 파일 및 관리
- 파일: `%APPDATA%\Renamer\stats_log.json`
- 60일 초과 항목 자동 삭제 (기록 시마다 정리)

### 표시 수치

**섹션 1 — 기준일 이후 누적** (통계 초기화 버튼으로 기준일 갱신)
- 분석 건수: `statsResetDate` 이후의 StatEntry 수
- 파일 이름 수정 건수: `statsResetDate` 이후 `WasRenamed == true` 수
- 누적 API 비용: `statsResetDate` 이후 `Cost` 합산

**섹션 2 — 지난 30일 간의** (통계 초기화 영향 없음)
- 기준: `DateTime.Today.AddDays(-30)` (오늘 자정에서 30일 전)
- 동일하게 분석 건수 / 수정 건수 / 누적 비용

### statsResetDate
- 기본값: `DateTime.MinValue` → UI에 "처음부터"로 표시
- 값 있으면 `yy. MM. dd.` 형식으로 표시 (예: `26. 06. 07.`)
- 저장 위치: 설정 파일

---

## 설정 항목

| 키 | 타입 | 기본값 |
|----|------|--------|
| `anthropicAPIKey` | string | `""` |
| `selectedModel` | string | `"claude-haiku-4-5-20251001"` |
| `enablePDF` | bool | `true` |
| `enableImage` | bool | `true` |
| `articleTemplate` | string | `"{name}({year}), {title}"` |
| `posterTemplate` | string | `"(포스터){title}({when: YYMMDD})"` |
| `maxLogCount` | int | `100` (선택지: 100/250/500/1000) |
| `statsResetDate` | DateTime | `DateTime.MinValue` |

설정 파일: `%APPDATA%\Renamer\settings.json`

---

## UI 구성

### 메인 창 (트레이 아이콘 클릭 시 표시)
- **상단**: 상태 표시 (동작 중 → 초록 원 / 동작 중지 → 노란 원) + 시작/중지 버튼
  - API Key 없으면 버튼 대신 "API 키 필요" 텍스트
- **중단**: 활동 로그 스크롤 목록 (타임스탬프 `오전/오후 HH:mm` + 메시지)
- **하단**: [설정] [통계] [종료] 버튼

### 통계 팝업 ([통계] 버튼 클릭)
```
분석 건수                   {n}건
파일 이름 수정 건수           {n}건
누적 API 비용               ${n.nnnn}
기준일: {날짜}     [카운팅 초기화]
────────────────────────────
지난 30일 간의
분석 건수                   {n}건
파일 이름 수정 건수           {n}건
누적 API 비용               ${n.nnnn}
```

### 설정 창
1. Claude API Key 입력 + [검증 및 저장] 버튼
2. 모델 선택: Haiku 4.5 / Sonnet 4.6 (세그먼트/라디오)
3. 적용 대상: PDF 토글, 이미지 토글
4. 파일 이름 규칙: 논문 템플릿 + [초기화] / 포스터 템플릿 + [초기화]
5. 로그: 최대 보관 건수 드롭다운 + [로그 초기화] 버튼

---

## 개발 시 주의사항

- API 호출은 반드시 `async/await` 비동기 처리
- 다운로드 폴더 경로: `Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads"`
- 파일이 완전히 쓰여진 후 처리되도록 짧은 지연 또는 파일 잠금 확인 필요
- 앱 시작 시 API Key가 있으면 자동으로 모니터링 시작
- 통계(`stats_log.json`)와 로그(`activity_log.json`)는 반드시 별도 파일로 분리
- 재시도 시 파일 존재 여부 재확인 후 진행
- 파일명에 사용 금지 문자 정제는 이름 변경 직전에 적용
- 앱 종료 시 트레이 아이콘 명시적 제거 (Windows 트레이 잔상 방지)
