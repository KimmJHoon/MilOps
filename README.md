# MilOps - 군-지자체 일정관리 시스템

군부대와 지자체 간 일정 조율 워크플로우를 디지털화한 애플리케이션입니다.

양측 담당자가 가용 시간을 입력하고, 시간대를 예약한 뒤, 양측 모두 확인하면 일정이 확정되는 4단계 상태 기반의 일정 관리 시스템입니다.

## 주요 기능

- **일정 워크플로우** : 생성 → 가용시간 입력 → 예약 → 양측 확정의 4단계 상태 관리
- **6개 역할 기반 접근 제어** : 역할별 화면 분기 및 권한 관리
- **실시간 동기화** : Supabase Realtime을 통한 양측 담당자 간 즉시 반영
- **푸시 알림** : Firebase Cloud Messaging(FCM)으로 일정 상태 변경 알림
- **캘린더 뷰** : 월간 캘린더에서 일정 시각적 확인
- **초대 시스템** : 초대코드 기반 사용자 온보딩
- **크로스플랫폼** : 단일 코드베이스로 Windows Desktop + Android 지원

## 기술 스택

| 구분 | 기술 |
|------|------|
| **Framework** | .NET 8.0 / C# |
| **UI** | Avalonia UI 11.3 |
| **Architecture** | MVVM (CommunityToolkit.Mvvm) |
| **Backend** | Supabase (PostgreSQL + GoTrue Auth + Realtime) |
| **Push Notification** | Firebase Cloud Messaging |

## 프로젝트 구조

```
MilOps/
├── MilOps/                    # 공유 핵심 라이브러리
│   ├── Views/                 # Avalonia XAML 뷰 (17개)
│   ├── ViewModels/            # MVVM 뷰모델 (16개)
│   ├── Models/                # Supabase 테이블 매핑 모델 (15개)
│   ├── Services/              # 비즈니스 로직 및 데이터 접근 (10개)
│   ├── Converters/            # XAML 값 변환기
│   └── Config/                # 환경 설정
├── MilOps.Desktop/            # Windows Desktop 진입점
├── MilOps.Android/            # Android 플랫폼
```

## 아키텍처

```
┌─────────────────────────────────────┐
│            Views (Avalonia XAML)    │
│          Data Binding / Commands    │
├─────────────────────────────────────┤
│            ViewModels               │
│     ObservableProperty + Relay      │
├─────────────────────────────────────┤
│            Services                 │
│  Auth / Schedule / Calendar / FCM   │
├─────────────────────────────────────┤
│            Models (Postgrest ORM)   │
├─────────────────────────────────────┤
│     Supabase (PostgreSQL + Auth     │
│       + Realtime + RLS)             │
└─────────────────────────────────────┘
```

## 일정 상태 관리 단계

```
 created ──→ inputted ──→ reserved ──→ confirmed
 (생성됨)     (입력됨)     (예약됨)     (확정됨)

 [지자체담당자]  [대대담당자]  [양측 확인]
  가용시간 입력   시간대 선택   LocalConfirmed = true
                              MilitaryConfirmed = true
                              → 양측 모두 true일 때 확정
```

## 역할 및 권한

| 역할 | 구분 | 접근 가능 기능 |
|------|------|---------------|
| **최종관리자 (행정안전부)** | 지자체 측 | 전체 조회, 담당자 관리 |
| **최종관리자 (육군본부)** | 군 측 | 전체 조회, 담당자 관리 |
| **지자체(도)** | 지자체 측 | 관할 지역 일정 조회, 담당자 관리 |
| **사단담당자** | 군 측 | 관할 부대 일정 조회, 담당자 관리 |
| **지자체담당자** | 지자체 측 | 일정 생성, 가용시간 입력, 확정 |
| **대대담당자** | 군 측 | 시간대 예약, 확정 |

## 시작하기

### 사전 요구사항

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Android 다운로드
+[1.0.0 version APK 다운로드](https://github.com/KimmJHoon/MilOps/releases/latest)

### Desktop 실행

```bash
dotnet run --project MilOps.Desktop
```



### [데이터베이스 스키마 LINK](https://github.com/KimmJHoon/MilOps/issues/2)

