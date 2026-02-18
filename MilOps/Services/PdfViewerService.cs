using System;

namespace MilOps.Services;

/// <summary>
/// PDF 뷰어 서비스 (플랫폼별 delegate 패턴)
/// Android: MainActivity에서 Intent를 통해 PDF 뷰어 앱으로 열기
/// </summary>
public static class PdfViewerService
{
    /// <summary>
    /// 플랫폼별 PDF 열기 액션 (Android에서 설정)
    /// 파라미터: EmbeddedResource 이름
    /// </summary>
    public static Action<string>? OpenPdfFromAssets { get; set; }

    /// <summary>
    /// 유저 매뉴얼 PDF 열기
    /// </summary>
    public static void OpenManual()
    {
        OpenPdfFromAssets?.Invoke("MilOps_유저_매뉴얼.pdf");
    }
}
