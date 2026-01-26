using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace MilOps.Converters;

/// <summary>
/// 탭 배경색 컨버터 (선택됨: 밝은색, 미선택: 투명)
/// </summary>
public class BoolToTabBgConverter : IValueConverter
{
    public static readonly BoolToTabBgConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#333333"));
    private static readonly IBrush DefaultBrush = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 탭 전경색 컨버터 (선택됨: 흰색, 미선택: 회색)
/// </summary>
public class BoolToTabFgConverter : IValueConverter
{
    public static readonly BoolToTabFgConverter Instance = new();

    private static readonly IBrush SelectedBrush = Brushes.White;
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#888888"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 시간 슬롯 배경색 컨버터 (선택됨: 파란색, 미선택: 투명)
/// </summary>
public class BoolToSlotBgConverter : IValueConverter
{
    public static readonly BoolToSlotBgConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#2196F3"));
    private static readonly IBrush DefaultBrush = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 시간 슬롯 테두리 컨버터 (선택됨: 파란색, 미선택: 회색)
/// </summary>
public class BoolToSlotBorderConverter : IValueConverter
{
    public static readonly BoolToSlotBorderConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#2196F3"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#555555"));

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
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#AAAAAA"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 날짜 버튼 배경색 컨버터 (선택됨: 파란색, 미선택: 회색)
/// </summary>
public class BoolToDateBgConverter : IValueConverter
{
    public static readonly BoolToDateBgConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#2196F3"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#333333"));

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
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#AAAAAA"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 저장 버튼 색상 컨버터 (입력 탭: 파란색, 예약 탭: 초록색)
/// </summary>
public class BoolToSaveBtnColorConverter : IValueConverter
{
    public static readonly BoolToSaveBtnColorConverter Instance = new();

    private static readonly Color InputColor = Color.Parse("#2196F3"); // Blue
    private static readonly Color ReserveColor = Color.Parse("#4CAF50"); // Green

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

