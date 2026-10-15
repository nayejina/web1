# CloudConverter MIP 버전

시연판(`CloudConverter.exe`)과 같은 화면에서 **Microsoft Information Protection(MIP) SDK 로 실제 민감도 레이블과 보호를 적용**하는 버전입니다.
Microsoft 365 에 로그인해야 하므로 인터넷 연결이 필요하고, 오프라인 시연용이 아닙니다.

| | 시연판 | MIP 버전 |
|---|---|---|
| 실행 파일 | `CloudConverter.exe` (단일 파일) | `CloudConverterMip.exe` (폴더째 배포, `x64\` 에 MIP 네이티브 DLL) |
| 변환 | 실제 암호화 없음 (복사 + 시연 규칙) | MIP SDK 로 레이블/보호 적용 |
| 우클릭 메뉴 | CloudConverter(demo) | CloudConverter(MIP) |
| 데이터 폴더 | `%LOCALAPPDATA%\CloudConverterDemo` | `%LOCALAPPDATA%\CloudConverterMip` (로그, `mip_data` SDK 캐시) |
| 대상/저장 위치 | 내 드라이브 + 가상 OneDrive/SharePoint | 내 드라이브만 (실제 OneDrive/SharePoint 는 Microsoft Graph 연동 필요) |

두 버전은 한 PC 에 같이 설치해도 서로 영향을 주지 않습니다.

## 1. 사전 준비 (관리자)

1. **라이선스**: Microsoft 365 E3/E5 또는 Microsoft Purview Information Protection (AIP P1 이상)
2. **민감도 레이블**: Microsoft Purview 포털 → 정보 보호 → 레이블에서 레이블을 만들고(암호화를 적용할 레이블은 "암호화" 설정), 레이블 정책으로 사용자에게 게시
3. **앱 등록**: Microsoft Entra 관리 센터 → 앱 등록 → 새 등록
   - 지원되는 계정 유형: 이 조직 디렉터리의 계정만
   - 인증 → 플랫폼 추가 → **모바일 및 데스크톱 애플리케이션** → 리디렉션 URI `http://localhost`
   - 인증 → 고급 설정 → **퍼블릭 클라이언트 흐름 허용: 예**
   - API 사용 권한 (위임된 권한) 추가 후 **관리자 동의 부여**
     - Microsoft Graph → `User.Read`
     - Azure Rights Management Services → `user_impersonation`
     - Microsoft Information Protection Sync Service → `UnifiedPolicy.User.Read`
       (목록에 없으면 "내 조직에서 사용하는 API" 에서 검색)
   - 개요 화면의 **애플리케이션(클라이언트) ID**, **디렉터리(테넌트) ID** 를 기록

## 2. 설정 (`config\mip-settings.json`)

```json
{
  "ClientId": "앱 등록의 애플리케이션(클라이언트) ID",
  "TenantId": "디렉터리(테넌트) ID 또는 contoso.onmicrosoft.com",
  "RedirectUri": "http://localhost",
  "ApplicationName": "CloudConverter",
  "LabelId": "",
  "AskLabelEveryTime": false,
  "JustificationMessage": "CloudConverter 를 통한 레이블 변경",
  "RemoveLabelOnDrmConversion": false,
  "LogLevel": "Warning"
}
```

| 항목 | 설명 |
|---|---|
| `LabelId` | MIP 변환 시 적용할 레이블 ID. 비우면 처음 변환할 때 **레이블 선택 창**이 뜨고, 고른 레이블은 프로그램을 닫을 때까지 유지 |
| `AskLabelEveryTime` | `true` 면 변환할 때마다 레이블 선택 창 |
| `JustificationMessage` | 레이블을 낮추거나 제거할 때 남기는 사유 |
| `RemoveLabelOnDrmConversion` | "DRM으로 변환" 시 보호 해제와 함께 레이블도 제거 |
| `LogLevel` | MIP SDK 로그 수준 (`Trace`/`Info`/`Warning`/`Error`). SDK 로그는 `%LOCALAPPDATA%\CloudConverterMip\mip_data` |

## 3. 사용

1. `CloudConverterMip.exe` 실행 → 파일 추가(드래그 앤 드롭 / 찾기 / 탐색기 우클릭 `CloudConverter(MIP)`) → 저장 위치 선택 → 변환 시작
2. 처음 변환할 때 **기본 브라우저에서 Microsoft 365 로그인** → 레이블 선택 → 변환 진행
3. 결과·실패 사유는 변환 상태 툴팁과 로그 보기(상세 메시지)에서 확인

### 변환 동작

| 변환 | 동작 |
|---|---|
| MIP로 변환 | 선택한 레이블 적용. 레이블에 암호화가 설정되어 있으면 보호도 적용. 확장자는 SDK 규칙(Office·PDF 유지, `.txt` → `.ptxt` 등) |
| DRM으로 변환 | **MIP 보호 해제**만 수행 (보호 해제 권한 필요). 3rd party DRM 암호화는 DRM 업체 SDK 연동이 필요해 포함되어 있지 않음 |

### 결과 코드 (MIP 버전에서 추가/의미가 바뀌는 것)

| 코드 | 결과 | 의미 |
|---|---|---|
| 1000 / 1001 | 성공 | 레이블 적용 / 보호 해제 성공 |
| 2001 | 실패 | 같은 레이블이 이미 적용된 파일 |
| 2003 | 실패 | 이미 MIP 로 보호된 파일 (상세 메시지에 보호 소유자) |
| 2004 | 실패 | 파일 접근 실패 |
| 2005 | 실패 | 권한 없음 (`NoPermissions`, `AccessDenied`, `PrivilegedRequired`) |
| 2007 | 실패 | 기타 MIP 오류 (상세 메시지에 SDK 예외 이름과 내용) |
| 2008 | 실패 | 네트워크 오류 |
| 2009 | 실패 | 레이블 변경 사유 필요 |
| 3001 | 스킵 | 지원하지 않는 형식 |
| 3006 | 스킵 | (DRM으로 변환) MIP 보호가 없는 파일 |

## 4. 확인되지 않은 부분 / 남은 작업

- 이 버전은 MIP SDK 1.18 API 에 맞춰 **빌드까지 확인**했고, 실제 테넌트에서의 로그인·레이블 적용은 테스트 테넌트에서 확인이 필요합니다.
- 3rd party DRM ↔ MIP 변환 (DRM 업체 SDK 로 복호화/암호화), 실제 OneDrive/SharePoint (Microsoft Graph) 연동은 포함되어 있지 않습니다.
- 앱 서명이 없으므로 SmartScreen 경고가 뜰 수 있습니다.

## 빌드

```powershell
.\build.ps1 -Mip    # dist\CloudConverterMip\ , dist\CloudConverterMip.zip
```
또는 `dotnet publish src/CloudConverter.App -c Release -r win-x64 --self-contained true -p:Edition=Mip -o <출력 폴더>`
