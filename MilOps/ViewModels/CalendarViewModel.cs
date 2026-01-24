using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace MilOps.ViewModels;

public partial class CalendarViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _currentYear;

    [ObservableProperty]
    private int _currentMonth;

    [ObservableProperty]
    private string _currentMonthYear = "";

    [ObservableProperty]
    private ObservableCollection<CalendarDay> _days = new();

    public CalendarViewModel()
    {
        var today = DateTime.Today;
        CurrentYear = today.Year;
        CurrentMonth = today.Month;
        UpdateCalendar();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        if (CurrentMonth == 1)
        {
            CurrentMonth = 12;
            CurrentYear--;
        }
        else
        {
            CurrentMonth--;
        }
        UpdateCalendar();
    }

    [RelayCommand]
    private void NextMonth()
    {
        if (CurrentMonth == 12)
        {
            CurrentMonth = 1;
            CurrentYear++;
        }
        else
        {
            CurrentMonth++;
        }
        UpdateCalendar();
    }

    private void UpdateCalendar()
    {
        CurrentMonthYear = $"{CurrentYear}년 {CurrentMonth}월";

        Days.Clear();

        var firstDay = new DateTime(CurrentYear, CurrentMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(CurrentYear, CurrentMonth);

        // 첫째 날의 요일 (일요일=0)
        int startDayOfWeek = (int)firstDay.DayOfWeek;

        // 이전 달의 빈 칸
        for (int i = 0; i < startDayOfWeek; i++)
        {
            Days.Add(new CalendarDay { Day = 0, IsCurrentMonth = false });
        }

        // 현재 달의 날짜
        var today = DateTime.Today;
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(CurrentYear, CurrentMonth, day);
            Days.Add(new CalendarDay
            {
                Day = day,
                IsCurrentMonth = true,
                IsToday = date == today,
                IsSunday = date.DayOfWeek == DayOfWeek.Sunday,
                IsSaturday = date.DayOfWeek == DayOfWeek.Saturday
            });
        }

        // 다음 달의 빈 칸 (6주 = 42칸 채우기)
        while (Days.Count < 42)
        {
            Days.Add(new CalendarDay { Day = 0, IsCurrentMonth = false });
        }
    }
}

public class CalendarDay
{
    public int Day { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public bool IsSunday { get; set; }
    public bool IsSaturday { get; set; }

    public string DayText => Day > 0 ? Day.ToString() : "";

    public string DayColor
    {
        get
        {
            if (IsToday) return "#00FF00";
            if (IsSunday) return "#FF6B6B";
            if (IsSaturday) return "#6B9FFF";
            return "White";
        }
    }
}
