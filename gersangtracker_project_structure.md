# GersangTracker (거상 트래커) 프로젝트 구조 분석

## 1. Project Overview (프로젝트 개요)
- **설명**: 거상(Gersang) 게임 플레이 중 사냥 시 드랍되는 아이템을 OCR 기술로 실시간 자동 감지하고, 기록을 바탕으로 총수익 및 시간당 수익을 계산해 주는 데스크탑 애플리케이션입니다.
- **주요 목적**: 사냥 수익 기록 자동화 및 데이터 시각화를 통한 편의성 제공

## 2. Technical Stack (기술 스택)
- **Framework & UI**: .NET 10, WPF (Windows Presentation Foundation)
- **Architecture**: MVVM (Model-View-ViewModel) 패턴 (`CommunityToolkit.Mvvm` 사용)
- **Database**: SQLite (`Microsoft.EntityFrameworkCore.Sqlite` 사용)
- **Computer Vision & OCR**:
  - `OpenCvSharp4`: 화면 캡처 및 이미지 전처리 (그레이스케일, 이진화 등)
  - `Tesseract` (v5.2.0): 이미지 내 텍스트 인식 (kor.traineddata 활용)
- **기타 라이브러리**: `EPPlus` (엑셀 내보내기 기능)

## 3. System Architecture (시스템 아키텍처)
MVVM 패턴을 철저히 분리하여 설계된 아키텍처를 가집니다.
- **Models**: `Monster`, `Session`, `DropLog`, `ItemPrice`, `MonsterItem` 등 SQLite DB의 테이블과 1:1 매핑되는 엔티티 모델
- **ViewModels**: `MainViewModel`, `HuntingViewModel`, `PriceViewModel` 등 UI와 데이터를 바인딩하며 비즈니스 로직을 연결하는 컴포넌트
- **Services**: 핵심 백그라운드 로직을 담당하는 클래스들
  - `OcrService.cs`: Win32 API를 활용한 창 캡처, OpenCV 전처리 및 Tesseract OCR 인식 수행
  - `DatabaseService.cs`: Entity Framework Core를 활용한 SQLite CRUD 로직 전담
  - `ExcelService.cs`: EPPlus를 이용해 사냥 세션 기록을 Excel 파일로 추출하는 기능 담당

## 4. Key Features (핵심 기능)
- **OCR 기반 실시간 드랍 자동 감지**: 게임 창을 백그라운드에서 추적하며, 드랍 메시지 영역을 1초 주기로 캡처하여 아이템명 및 수량 추출.
- **오인식 자동 보정 (레벤슈타인 거리)**: 인게임 폰트와 배경으로 인한 OCR 오타를 자체 등록된 몬스터 드랍 아이템 목록과 비교하여 자동 보정.
- **사냥 수익 계산기**: 인식된 아이템에 사용자가 지정한 단가를 곱해 총수익 및 시간당 수익(Gold/h)을 실시간으로 산출.
- **세션 관리 및 통계**: 사냥 시작/종료를 하나의 세션으로 기록하며, 세션별 통계 조회 및 엑셀 내보내기 지원.
- **몬스터별 데이터 관리**: 몬스터마다 드랍되는 아이템 목록과 단가를 분리하여 저장 및 관리.

## 5. Technical Challenges & Troubleshooting (기술적 도전 및 해결)
- **인게임 폰트 인식률 저하 문제**:
  - **문제**: 게임 내 드랍 메시지 창의 배경이 투명하거나 폰트 크기가 작아 Tesseract OCR이 정확히 텍스트를 읽어내지 못함.
  - **해결**: `OcrService` 내에서 OpenCV를 활용. 드랍 메시지 출력 영역(`230x130`)만 정밀하게 크롭한 뒤, 이미지를 **3배 확대**(`InterpolationMode.HighQualityBicubic`)하고, **그레이스케일 변환 및 이진화(Threshold)**를 거쳐 노이즈를 제거하여 인식률을 극적으로 향상시킴.
- **OCR 텍스트 오타 및 매칭 문제**:
  - **문제**: 전처리를 거쳐도 '{' 가 '[' 로 인식되거나, 특수문자와 한글이 섞여 이상하게 읽히는 경우 발생.
  - **해결**: 정규식을 통해 `[아이템명]` 패턴에서 한글만 우선 추출한 뒤, **레벤슈타인 거리(Levenshtein Distance) 알고리즘**을 적용. 인식된 단어와 DB에 등록된 대상 아이템들 간의 유사도를 계산하여, 임계값(글자수의 50% 또는 3자 이하 차이)을 통과하는 가장 근접한 아이템으로 자동 매핑(보정) 처리함.
- **로그 중복 스캔 방지**:
  - **문제**: 1초마다 화면을 캡처하므로 동일한 드랍 메시지가 여러 번 감지될 수 있음.
  - **해결**: 이전 캡처의 텍스트 줄(Line)과 현재 캡처된 줄을 비교하여 신규 추가된 줄만 추출하고, 직전 캡처의 확정 줄(`_lastConfirmedLines`)과도 교차 검증하는 로직을 추가해 중복 카운트를 원천 차단함.

## 6. Core Logic & Optimization (핵심 로직 및 최적화)
- **Win32 API 기반 논블로킹 캡처**:
  - `FindWindow`, `GetWindowRect`, `BitBlt` 등 Windows API를 DllImport로 직접 호출하여, 화면 전체를 캡처하는 대신 해당 게임 창의 디바이스 컨텍스트(DC)에서 데이터를 고속으로 복사. 시스템 부하(CPU 사용량)를 최소화.
- **데이터 병합 및 최적화된 동기화**:
  - `DatabaseService.cs`의 `SyncDropLogsAsync` 로직을 통해 사용자가 기록된 드랍 아이템 수량을 수동으로 수정할 경우, 파편화된 다수의 드랍 로그(`DropLog`)를 한 개의 통합된 로그로 병합(Merge)하여 DB 용량을 아끼고 통계 조회 쿼리 성능을 최적화.

## 7. Retrospective (회고 및 성과)
- C#과 WPF를 활용하여 안정적인 데스크탑 애플리케이션을 성공적으로 구축.
- OpenCV 기반의 이미지 전처리와 레벤슈타인 거리 알고리즘의 결합을 통해, 노이즈가 많은 게임 화면 위에서도 신뢰성 높은 실시간 텍스트 추출 파이프라인을 완성한 점이 주요 성과임.
- EF Core와 SQLite를 통해 로컬 환경에서도 구조적이고 무결성이 보장되는 데이터 관리를 구현함.

## 8. Links (관련 링크)
- **Releases**: [GersangTracker Releases](https://github.com/JIK-K/GersangTracker/releases)
- **Wiki**: [GersangTracker Wiki](https://github.com/JIK-K/GersangTracker/wiki)

---

## 9. UI/Design Changelog (디자인 변경 이력)

### v1.0.3 — 디자인 개선 (2026-05-16)

#### 9-1. 전역 디자인 시스템 강화 (`App.xaml`)
- **새 색상 토큰 추가**
  - `BackgroundQuaternary` (`#333848`): SecondaryButton hover 전용 배경
  - `GlassOverlay` (`#0AFFFFFF`): 리스트 행 hover 오버레이
  - `SuccessGradient`: 초록 계열 그라디언트 (향후 사용 대비)
- **`SecondaryButton` hover 버그 수정**: 기존에는 hover 시 색상이 동일하여 반응이 없었음. `BackgroundQuaternary`로 교체하여 실질적인 피드백 제공
- **새 공유 스타일 추가**
  - `ModernTextBox`: CornerRadius 6, 포커스 시 Accent 테두리 활성화
  - `SectionHeaderLabel`: 섹션 제목용 TextBlock (TextSecondary, 11pt Bold)
  - `PanelTitle`: 패널 내부 제목용 TextBlock (TextPrimary, 11pt Bold)
- **`CardHoverShadow` Effect 정의** (`App.xaml`): 청록 그림자 Effect 리소스를 선언만 해둔 상태. 실제 `MainWindow`의 카드 hover에는 DataTemplate 내 인라인 `DropShadowEffect` (`x:Name="cardShadow"`)로 직접 구현되어 있으며, 트리거에서 해당 인스턴스의 Color/Opacity/BlurRadius를 동적으로 변경하는 방식으로 동작함
- **스크롤바 슬림화**: Width 8px → 6px, thumb 색상 반투명 흰색으로 세련화

#### 9-2. MainWindow — 카드 & 사이드바 개선
- **몬스터 카드 hover 효과**: 마우스오버 시 배경색이 `BackgroundTertiary`로 전환되고 청록 그림자 적용
- **카드 헤더 아이콘 장식**: 헤더 우측에 반투명 Sword 아이콘 추가
- **사이드바 하단 카운터 카드**: `Monsters.Count`를 실시간으로 표시하는 통계 카드 추가
- **콘텐츠 헤더 개선**: 하단 separator 라인 추가, 우측에 몬스터 개수 표시
- **텍스트박스**: 인라인 스타일 → `ModernTextBox` 전역 스타일로 교체

#### 9-3. HuntingWindow — 색상 통일 및 헤더 재디자인
- **색상 통일**: 하드코딩된 `#222831`, `#393E46`, `#2D3139`, `#EEEEEE` 전부 전역 리소스(`BackgroundSecondary`, `BackgroundTertiary`, `BackgroundPrimary`, `TextPrimary`)로 교체
- **헤더 재디자인**: 단순 Dark 배경 → `AccentGradient` 그라디언트 배경으로 변경
- **경과 시간 배지**: 우측 상단에 반투명 배지 형태로 표시, 가독성 향상
- **섹션 헤더 아이콘**: 드랍 로그 / 아이템 합산 섹션에 아이콘 추가
- **하단 버튼 영역**: separator 라인 추가

#### 9-4. PriceWindow — 색상 통일 및 UI 정리
- **색상 통일**: 전역 리소스로 교체
- **몬스터명 배지**: 헤더 우측에 `BackgroundPrimary` 배경 배지로 표시
- **컬럼 헤더**: `Opacity` 직접 지정 방식 → `SectionHeaderLabel` 스타일로 통일
- **하단 버튼 영역**: separator 라인 추가

#### 9-5. ResultWindow — 수익 요약 카드 재디자인
- **색상 통일**: 전역 리소스로 교체
- **수익 요약 패널 재디자인**: 기존 3행 그리드 → 좌우 분할 카드 레이아웃
  - 좌측: 총 수익 / 사냥 시간 (스택 형태)
  - 우측: 시간당 수익을 `AccentGradient` 배경 하이라이트 카드로 강조, TrendingUp 아이콘 추가
  - 중간에 `SeparatorColor` Divider 라인 삽입
- **헤더**: 몬스터명 배지 추가

#### 9-6. SessionWindow — hover 인터랙션
- **세션 행 hover**: `GlassOverlay` 반투명 배경 overlay 적용
- **몬스터명 배지**: 헤더 우측 배지 형태로 표시
- **컬럼 헤더**: `SectionHeaderLabel` 스타일 통일

#### 9-7. MonsterItemDialog — 커스텀 타이틀바 전환
- **OS 기본 창 → 커스텀 창**: `AnimatedWindowStyle` 적용, 커스텀 타이틀바 추가 (다른 창과 동일한 패턴)
- **버튼**: 인라인 스타일 → `PrimaryButton`, `SecondaryButton` 전역 스타일로 교체
- **TextBox**: `ModernTextBox` 스타일 적용
- **아이템 행**: hover 시 `GlassOverlay` 효과 추가
- **Code-behind**: `TitleBar_MouseLeftButtonDown` 드래그 핸들러 추가

#### 9-8. RenameDialog — 커스텀 타이틀바 전환
- **OS 기본 창 → 커스텀 창**: `AnimatedWindowStyle` 적용, 커스텀 타이틀바 추가
- **버튼**: 인라인 스타일 → `SecondaryButton` / `PrimaryButton` 전역 스타일로 교체
- **TextBox**: `ModernTextBox` 스타일 적용
- **Code-behind**: `TitleBar_MouseLeftButtonDown` 드래그 핸들러 추가
