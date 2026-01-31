using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace MilOps.Converters;

/// <summary>
/// 상태 필터 버튼의 배경색을 결정하는 컨버터
/// 선택된 필터면 강조색, 아니면 기본색 반환
/// </summary>
public class StatusFilterConverter : IValueConverter
{
    public static readonly StatusFilterConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#2196F3"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#333333"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedFilter && parameter is string buttonFilter)
        {
            return selectedFilter == buttonFilter ? SelectedBrush : DefaultBrush;
        }
        return DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
