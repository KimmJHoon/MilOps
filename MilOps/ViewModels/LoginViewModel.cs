using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Services;
using System;
using System.Threading.Tasks;

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

    [ObservableProperty]
    private bool _isLoading = false;

    public event Action? LoginSuccessful;

    [RelayCommand]
    private async Task LoginAsync()
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

        IsLoading = true;

        try
        {
            // Supabase 초기화
            if (!SupabaseService.IsInitialized)
            {
                await SupabaseService.InitializeAsync();
            }

            // Supabase Auth로 로그인
            var (success, errorMessage) = await AuthService.LoginAsync(UserId, Password);

            if (success)
            {
                LoginSuccessful?.Invoke();
            }
            else
            {
                ErrorMessage = errorMessage ?? "로그인에 실패했습니다";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"오류가 발생했습니다: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
