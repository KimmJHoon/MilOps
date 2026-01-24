using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

public partial class InviteAcceptViewModel : ViewModelBase
{
    private readonly HttpClient _httpClient = new();
    private string _inviteCode = "";

    // 초대 정보 (validate-invitation에서 받아옴)
    [ObservableProperty]
    private string _inviteeName = "";

    [ObservableProperty]
    private string _inviteeRole = "";

    [ObservableProperty]
    private string _inviteeAffiliation = "";

    [ObservableProperty]
    private string _inviteePhone = "";

    [ObservableProperty]
    private string _remainingTime = "";

    [ObservableProperty]
    private string _remainingDays = "";

    // 사용자 입력
    [ObservableProperty]
    private string _loginId = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _passwordConfirm = "";

    // 상태
    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _isValidating = true;

    [ObservableProperty]
    private bool _isInviteValid = false;

    [ObservableProperty]
    private bool _isLoginIdAvailable = false;

    [ObservableProperty]
    private string _loginIdCheckMessage = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _hasError = false;

    // 이벤트
    public event Action? RegistrationSuccessful;
    public event Action? NavigateToLogin;

    /// <summary>
    /// 이미 검증된 Invitation 객체로 초기화 (InviteCodeView에서 검증 후 전달)
    /// </summary>
    public void InitializeWithInvitation(Invitation invitation)
    {
        _inviteCode = invitation.InviteCode;
        InviteeName = invitation.Name;
        InviteeRole = invitation.RoleDisplayName;
        InviteeAffiliation = invitation.DisplayAffiliation ?? "";
        InviteePhone = invitation.Phone ?? "";

        // 남은 시간 계산
        var remaining = invitation.ExpiresAt - DateTime.UtcNow;
        var days = (int)remaining.TotalDays;
        RemainingDays = days.ToString();

        if (days >= 1)
        {
            RemainingTime = $"{days}일 후 만료됩니다";
        }
        else if (remaining.TotalHours >= 1)
        {
            RemainingTime = $"{(int)remaining.TotalHours}시간 후 만료됩니다";
        }
        else
        {
            RemainingTime = "곧 만료됩니다";
        }

        IsValidating = false;
        IsInviteValid = true;
        HasError = false;
        ErrorMessage = "";

        // 입력 필드 초기화
        LoginId = "";
        Password = "";
        PasswordConfirm = "";
        LoginIdCheckMessage = "";
        IsLoginIdAvailable = false;

        System.Diagnostics.Debug.WriteLine($"[InviteAccept] Initialized with invitation: {invitation.InviteCode}, Name: {invitation.Name}");
    }

    /// <summary>
    /// 초대 코드로 초기화 및 유효성 검증 (딥링크에서 사용)
    /// </summary>
    public async Task InitializeWithInviteCodeAsync(string inviteCode)
    {
        _inviteCode = inviteCode;
        IsValidating = true;
        HasError = false;
        ErrorMessage = "";

        try
        {
            if (!SupabaseService.IsInitialized)
            {
                await SupabaseService.InitializeAsync();
            }

            // Edge Function 호출: validate-invitation
            var supabaseUrl = GetSupabaseUrl();
            var anonKey = GetSupabaseAnonKey();
            var requestUrl = $"{supabaseUrl}/functions/v1/validate-invitation";

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", anonKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { invite_code = inviteCode }),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"[InviteAccept] validate-invitation response: {responseJson}");

            var result = JsonSerializer.Deserialize<ValidateInvitationResponse>(responseJson);

            if (result?.valid == true && result.invitation != null)
            {
                InviteeName = result.invitation.name ?? "";
                InviteeRole = result.invitation.role_name ?? result.invitation.role ?? "";
                InviteeAffiliation = result.invitation.affiliation ?? "";
                RemainingTime = result.invitation.remaining_time ?? "";
                IsInviteValid = true;
            }
            else
            {
                ErrorMessage = result?.error ?? "유효하지 않거나 만료된 초대 코드입니다.";
                HasError = true;
                IsInviteValid = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InviteAccept] Error: {ex.Message}");
            ErrorMessage = "초대 코드 확인 중 오류가 발생했습니다.";
            HasError = true;
            IsInviteValid = false;
        }
        finally
        {
            IsValidating = false;
        }
    }

    /// <summary>
    /// 아이디 중복 확인
    /// </summary>
    [RelayCommand]
    private async Task CheckLoginIdAsync()
    {
        if (string.IsNullOrWhiteSpace(LoginId))
        {
            LoginIdCheckMessage = "아이디를 입력하세요";
            IsLoginIdAvailable = false;
            return;
        }

        if (LoginId.Length < 4)
        {
            LoginIdCheckMessage = "아이디는 4자 이상이어야 합니다";
            IsLoginIdAvailable = false;
            return;
        }

        try
        {
            var existingUser = await SupabaseService.GetUserByLoginIdAsync(LoginId);
            if (existingUser != null)
            {
                LoginIdCheckMessage = "이미 사용 중인 아이디입니다";
                IsLoginIdAvailable = false;
            }
            else
            {
                LoginIdCheckMessage = "사용 가능한 아이디입니다";
                IsLoginIdAvailable = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InviteAccept] CheckLoginId Error: {ex.Message}");
            LoginIdCheckMessage = "확인 중 오류가 발생했습니다";
            IsLoginIdAvailable = false;
        }
    }

    /// <summary>
    /// 회원가입 처리
    /// </summary>
    [RelayCommand]
    private async Task RegisterAsync()
    {
        HasError = false;
        ErrorMessage = "";

        // 입력 검증
        if (string.IsNullOrWhiteSpace(LoginId) || LoginId.Length < 4)
        {
            ErrorMessage = "아이디는 4자 이상이어야 합니다";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
        {
            ErrorMessage = "비밀번호는 8자 이상이어야 합니다";
            HasError = true;
            return;
        }

        if (Password != PasswordConfirm)
        {
            ErrorMessage = "비밀번호가 일치하지 않습니다";
            HasError = true;
            return;
        }

        if (!IsLoginIdAvailable)
        {
            ErrorMessage = "아이디 중복 확인을 해주세요";
            HasError = true;
            return;
        }

        IsLoading = true;

        try
        {
            // Edge Function 호출: accept-invitation
            var supabaseUrl = GetSupabaseUrl();
            var anonKey = GetSupabaseAnonKey();
            var requestUrl = $"{supabaseUrl}/functions/v1/accept-invitation";

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", anonKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    invite_code = _inviteCode,
                    login_id = LoginId,
                    password = Password
                }),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"[InviteAccept] accept-invitation response: {responseJson}");

            var result = JsonSerializer.Deserialize<AcceptInvitationResponse>(responseJson);

            if (result?.success == true)
            {
                // 회원가입 성공
                RegistrationSuccessful?.Invoke();
            }
            else
            {
                ErrorMessage = result?.error ?? "회원가입에 실패했습니다";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InviteAccept] Register Error: {ex.Message}");
            ErrorMessage = "회원가입 중 오류가 발생했습니다";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 로그인 화면으로 이동
    /// </summary>
    [RelayCommand]
    private void GoToLogin()
    {
        NavigateToLogin?.Invoke();
    }

    private string GetSupabaseUrl()
    {
        // SupabaseConfig에서 URL 가져오기
        if (!string.IsNullOrEmpty(Config.SupabaseConfig.Url))
        {
            return Config.SupabaseConfig.Url;
        }
        return Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "";
    }

    private string GetSupabaseAnonKey()
    {
        // SupabaseConfig에서 Anon Key 가져오기
        if (!string.IsNullOrEmpty(Config.SupabaseConfig.AnonKey))
        {
            return Config.SupabaseConfig.AnonKey;
        }
        return Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? "";
    }

    // JSON 응답 모델
    private class ValidateInvitationResponse
    {
        public bool valid { get; set; }
        public string? error { get; set; }
        public InvitationInfo? invitation { get; set; }
    }

    private class InvitationInfo
    {
        public string? role { get; set; }
        public string? role_name { get; set; }
        public string? name { get; set; }
        public string? phone { get; set; }
        public string? affiliation { get; set; }
        public string? remaining_time { get; set; }
    }

    private class AcceptInvitationResponse
    {
        public bool success { get; set; }
        public string? error { get; set; }
        public string? user_id { get; set; }
        public string? login_id { get; set; }
    }
}
