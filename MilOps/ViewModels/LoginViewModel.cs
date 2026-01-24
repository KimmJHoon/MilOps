using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Services;
using System;

namespace MilOps.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _userId = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _hasError = false;

    public event Action? LoginSuccessful;

    [RelayCommand]
    private void Login()
    {
        HasError = false;
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(UserId))
        {
            ErrorMessage = "아이디를 입력하세요";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "비밀번호를 입력하세요";
            HasError = true;
            return;
        }

        // 하드코딩된 최종관리자 계정
        if (UserId == "SW0001" && Password == "qwer1234")
        {
            AuthService.Login(UserId);
            LoginSuccessful?.Invoke();
        }
        else
        {
            ErrorMessage = "아이디 또는 비밀번호가 일치하지 않습니다";
            HasError = true;
        }
    }
}
