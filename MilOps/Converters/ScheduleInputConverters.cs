using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace MilOps.Converters;

/// <summary>
/// 탭 배경색 컨버터 (선택됨: 프라이머리 다크, 미선택: 투명)
/// </summary>
public class BoolToTabBgConverter : IValueConverter
{
    public static readonly BoolToTabBgConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#488e72"));
    private static readonly IBrush DefaultBrush = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 탭 전경색 컨버터 (선택됨: 밝은색, 미선택: 회색)
/// </summary>
public class BoolToTabFgConverter : IValueConverter
{
    public static readonly BoolToTabFgConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#effdf6"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#6a6a6a"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 시간 슬롯 배경색 컨버터 (선택됨: 프라이머리, 미선택: 투명)
/// </summary>
public class BoolToSlotBgConverter : IValueConverter
{
    public static readonly BoolToSlotBgConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#00a872"));
    private static readonly IBrush DefaultBrush = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 시간 슬롯 테두리 컨버터 (선택됨: 프라이머리, 미선택: 보더)
/// </summary>
public class BoolToSlotBorderConverter : IValueConverter
{
    public static readonly BoolToSlotBorderConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#00a872"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#3a3a3c"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 시간 슬롯 전경색 컨버터 (선택됨: 흰색, 미선택: 회색)
/// </summary>
public class BoolToSlotFgConverter : IValueConverter
{
    public static readonly BoolToSlotFgConverter Instance = new();

    private static readonly IBrush SelectedBrush = Brushes.White;
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#a0a0a0"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 날짜 버튼 배경색 컨버터 (선택됨: 프라이머리, 미선택: 카드배경)
/// </summary>
public class BoolToDateBgConverter : IValueConverter
{
    public static readonly BoolToDateBgConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#00a872"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#2c2c2e"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 날짜 버튼 전경색 컨버터 (선택됨: 흰색, 미선택: 회색)
/// </summary>
public class BoolToDateFgConverter : IValueConverter
{
    public static readonly BoolToDateFgConverter Instance = new();

    private static readonly IBrush SelectedBrush = Brushes.White;
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#a0a0a0"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 저장 버튼 색상 컨버터 (입력 탭: 프라이머리 다크, 예약 탭: 프라이머리)
/// </summary>
public class BoolToSaveBtnColorConverter : IValueConverter
{
    public static readonly BoolToSaveBtnColorConverter Instance = new();

    private static readonly Color InputColor = Color.Parse("#488e72");
    private static readonly Color ReserveColor = Color.Parse("#00a872");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? InputColor : ReserveColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 저장 버튼 텍스트 컨버터 (입력 탭: "저장", 예약 탭: "예약하기")
/// </summary>
public class BoolToSaveBtnTextConverter : IValueConverter
{
    public static readonly BoolToSaveBtnTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "저장" : "예약하기";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

