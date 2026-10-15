using CloudConverter.Core;

namespace CloudConverter.App.Services;

/// <summary>변환 시작 전에 화면이 필요한 준비(로그인, 레이블 선택 등)를 하는 엔진</summary>
internal interface IPreparableEngine
{
    /// <returns>false 면 변환을 시작하지 않는다 (사용자가 취소했거나 준비 실패)</returns>
    Task<bool> PrepareAsync(ConversionFormat format, IWin32Window owner);

    /// <summary>로그인한 계정 (프로그램 정보 표시용). 로그인 전이면 null</summary>
    string? SignedInAccount { get; }
}
