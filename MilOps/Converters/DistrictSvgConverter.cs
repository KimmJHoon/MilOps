using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Svg.Skia;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MilOps.Converters;

/// <summary>
/// 한국어 지자체명 → SvgImage 변환 컨버터
/// 입력: "목포시", "진도군" 등 한국어 지자체명
/// 출력: avares://MilOps/Assets/{romanized}.svg 로 로드된 SvgImage
/// 한 번 로드한 SvgImage는 캐시하여 재파싱 방지
/// </summary>
public class DistrictSvgConverter : IValueConverter
{
    public static readonly DistrictSvgConverter Instance = new();

    // 한국어 지자체명 → 로마자 SVG 파일명 (확장자 제외)
    private static readonly Dictionary<string, string> DistrictMap = new()
    {
        // 전라남도
        ["목포시"] = "mokpo",
        ["여수시"] = "yeosu",
        ["순천시"] = "suncheon",
        ["나주시"] = "naju",
        ["광양시"] = "gwangyang",
        ["담양군"] = "damyang",
        ["곡성군"] = "gokseong",
        ["구례군"] = "goorye",
        ["보성군"] = "boseong",
        ["화순군"] = "hwasun",
        ["장흥군"] = "jangheung",
        ["강진군"] = "gangjin",
        ["해남군"] = "haenam",
        ["영암군"] = "yeongam",
        ["무안군"] = "mooan",
        ["장성군"] = "jangseong",
        ["완도군"] = "wando",
        ["신안군"] = "sinan",
        ["영광군"] = "yeonghwang",
        ["진도군"] = "jindo",
        ["함평군"] = "hampyeong",
        ["고흥군"] = "goheung",
        // 광주광역시
        ["서구"] = "seogu",
        ["북구"] = "bukgu",
        ["동구"] = "donggu",
        ["남구"] = "namgu",
        ["광산구"] = "gwangsangu",
    };

    // 캐시: 로마자명 → 로드된 IImage (SVG 반복 파싱 방지)
    private static readonly Dictionary<string, IImage?> _cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string koreanName || string.IsNullOrEmpty(koreanName))
            return null;

        if (!DistrictMap.TryGetValue(koreanName, out var romanized))
            return null;

        if (_cache.TryGetValue(romanized, out var cached))
            return cached;

        try
        {
            var svg = new SvgImage
            {
                Source = SvgSource.Load($"avares://MilOps/Assets/{romanized}.svg", null)
            };
            _cache[romanized] = svg;
            return svg;
        }
        catch
        {
            _cache[romanized] = null;
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
