namespace CloudConverter.Core;

/// <summary>
/// 파일 1개 변환 결과. Detail 은 로그 상세 보기에 남길 부가 메시지 (예: MIP SDK 오류 내용),
/// OutputPath 는 실제로 저장된 경로 (엔진이 확장자를 바꾼 경우. null 이면 요청한 경로)
/// </summary>
public readonly record struct ConversionOutcome(int Code, string Detail = "", string? OutputPath = null);

/// <summary>
/// 변환 엔진. 시연판은 <see cref="ConversionEngine"/>(실제 암호화 없음),
/// MIP 버전은 MIP SDK 로 실제 레이블/보호를 적용하는 엔진을 사용한다.
/// </summary>
public interface IConversionEngine
{
    /// <summary>목록의 "암호화" 컬럼(가능/불가) 판정</summary>
    Eligibility CheckEligibility(string filePath);

    /// <summary>파일 1개 변환 (백그라운드 스레드에서 호출됨)</summary>
    ConversionOutcome Convert(string sourcePath, string destinationPath, ConversionFormat format);
}
