using FluentAssertions;
using MilOps.Models;
using MilOps.Services;
using MilOps.Services.Abstractions;
using MilOps.ViewModels;
using Moq;
using Xunit;

namespace MilOps.Tests.ViewModels;

/// <summary>
/// ScheduleListViewModel 테스트
///
/// 6가지 불변조건 체크리스트:
/// 1. IsLoading은 실행 시작에 true, 끝나면 무조건 false (성공/실패 상관없이)
/// 2. 예외가 나면 에러 상태 처리 (현재 ErrorMessage 없음 - 개선 필요)
/// 3. 컬렉션(Schedules) 변경 규칙이 일정
/// 4. CanExecute가 상태에 따라 정확히 바뀜
/// 5. 중복 실행 방지 (TODO: 패턴 적용 후)
/// 6. 상태별 카운트가 정확히 계산됨
/// </summary>
public class ScheduleListViewModelTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ISupabaseService> _mockSupabaseService;

    public ScheduleListViewModelTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _mockSupabaseService = new Mock<ISupabaseService>();

        // 기본 설정: 초기화됨
        _mockSupabaseService.Setup(x => x.IsInitialized).Returns(true);
    }

    #region 헬퍼 메서드

    private User CreateTestUser(string role, Guid? id = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            LoginId = "test_user",
            Name = "테스트 사용자",
            Role = role,
            Email = "test@test.com"
        };
    }

    private Schedule CreateTestSchedule(string status, Guid? companyId = null, Guid? localUserId = null, Guid? militaryUserId = null)
    {
        return new Schedule
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? Guid.NewGuid(),
            LocalUserId = localUserId ?? Guid.NewGuid(),
            MilitaryUserId = militaryUserId ?? Guid.NewGuid(),
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    private ScheduleListViewModel CreateViewModel(bool autoInitialize = false)
    {
        return new ScheduleListViewModel(_mockAuthService.Object, _mockSupabaseService.Object, autoInitialize);
    }

    private void SetupEmptyCacheData()
    {
        _mockSupabaseService.Setup(x => x.GetActiveCompaniesAsync()).ReturnsAsync(new List<Company>());
        _mockSupabaseService.Setup(x => x.GetBattalionsAsync()).ReturnsAsync(new List<Battalion>());
        _mockSupabaseService.Setup(x => x.GetDistrictsAsync()).ReturnsAsync(new List<District>());
        _mockSupabaseService.Setup(x => x.GetActiveUsersAsync()).ReturnsAsync(new List<User>());
    }

    #endregion

    #region 1. IsLoading 상태 전이 테스트

    [Fact]
    public async Task LoadSchedulesAsync_SetsIsLoadingTrueAtStart()
    {
        // Arrange
        var user = CreateTestUser("user_local");
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);

        var tcs = new TaskCompletionSource<List<Schedule>>();
        _mockSupabaseService.Setup(x => x.GetSchedulesAsync()).Returns(tcs.Task);
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        var loadTask = vm.LoadSchedulesAsync();

        // Assert - 실행 중에는 IsLoading이 true
        vm.IsLoading.Should().BeTrue("로딩 시작 시 IsLoading은 true여야 함");

        // Cleanup
        tcs.SetResult(new List<Schedule>());
        await loadTask;
    }

    [Fact]
    public async Task LoadSchedulesAsync_SetsIsLoadingFalseOnSuccess()
    {
        // Arrange
        var user = CreateTestUser("user_local");
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);
        _mockSupabaseService.Setup(x => x.GetSchedulesAsync()).ReturnsAsync(new List<Schedule>());
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        await vm.LoadSchedulesAsync();

        // Assert
        vm.IsLoading.Should().BeFalse("성공 후 IsLoading은 false여야 함");
    }

    [Fact]
    public async Task LoadSchedulesAsync_SetsIsLoadingFalseOnException()
    {
        // Arrange
        var user = CreateTestUser("user_local");
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);
        _mockSupabaseService.Setup(x => x.GetSchedulesAsync()).ThrowsAsync(new Exception("DB 오류"));
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        await vm.LoadSchedulesAsync();

        // Assert
        vm.IsLoading.Should().BeFalse("예외 발생해도 IsLoading은 반드시 false여야 함");
    }

    #endregion

    #region 2. 사용자 없을 때 Early Return 테스트

    [Fact]
    public async Task LoadSchedulesAsync_WhenNoUser_ReturnsEarly()
    {
        // Arrange
        _mockAuthService.Setup(x => x.CurrentUser).Returns((User?)null);

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        await vm.LoadSchedulesAsync();

        // Assert
        vm.IsLoading.Should().BeFalse();
        _mockSupabaseService.Verify(x => x.GetSchedulesAsync(), Times.Never(),
            "사용자가 없으면 DB 호출하면 안 됨");
    }

    [Fact]
    public async Task LoadSchedulesAsync_WhenNotInitialized_ReturnsEarly()
    {
        // Arrange
        var user = CreateTestUser("user_local");
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);
        _mockSupabaseService.Setup(x => x.IsInitialized).Returns(false);

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        await vm.LoadSchedulesAsync();

        // Assert
        _mockSupabaseService.Verify(x => x.GetSchedulesAsync(), Times.Never(),
            "초기화 안 됐으면 DB 호출하면 안 됨");
    }

    #endregion

    #region 3. 역할별 필터링 테스트

    [Fact]
    public async Task LoadSchedulesAsync_UserLocal_FiltersOnlyOwnSchedules()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser("user_local", userId);
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);

        var mySchedule = CreateTestSchedule("created", localUserId: userId);
        var otherSchedule = CreateTestSchedule("created", localUserId: Guid.NewGuid());

        _mockSupabaseService.Setup(x => x.GetSchedulesAsync())
            .ReturnsAsync(new List<Schedule> { mySchedule, otherSchedule });
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        await vm.LoadSchedulesAsync();

        // Assert
        vm.AllCount.Should().Be(1, "user_local은 자신의 일정만 봐야 함");
    }

    [Fact]
    public async Task LoadSchedulesAsync_UserMilitary_ExcludesCreatedStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser("user_military", userId);
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);

        var createdSchedule = CreateTestSchedule("created", militaryUserId: userId);
        var inputtedSchedule = CreateTestSchedule("inputted", militaryUserId: userId);

        _mockSupabaseService.Setup(x => x.GetSchedulesAsync())
            .ReturnsAsync(new List<Schedule> { createdSchedule, inputtedSchedule });
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        await vm.LoadSchedulesAsync();

        // Assert
        vm.AllCount.Should().Be(1, "user_military는 created 상태 제외");
        vm.CreatedCount.Should().Be(0);
        vm.InputtedCount.Should().Be(1);
    }

    #endregion

    #region 4. 상태 필터 테스트

    [Fact]
    public async Task SetStatusFilter_FiltersSchedulesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser("user_local", userId);
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);

        var createdSchedule = CreateTestSchedule("created", localUserId: userId);
        var inputtedSchedule = CreateTestSchedule("inputted", localUserId: userId);
        var reservedSchedule = CreateTestSchedule("reserved", localUserId: userId);

        _mockSupabaseService.Setup(x => x.GetSchedulesAsync())
            .ReturnsAsync(new List<Schedule> { createdSchedule, inputtedSchedule, reservedSchedule });
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);
        await vm.LoadSchedulesAsync();

        // Act & Assert - 전체
        vm.Schedules.Count.Should().Be(3);

        // Act & Assert - created만
        vm.SetStatusFilterCommand.Execute("created");
        vm.Schedules.Count.Should().Be(1);
        vm.SelectedStatusFilter.Should().Be("created");

        // Act & Assert - 다시 전체
        vm.SetStatusFilterCommand.Execute("all");
        vm.Schedules.Count.Should().Be(3);
    }

    #endregion

    #region 5. 상태별 카운트 테스트

    [Fact]
    public async Task LoadSchedulesAsync_UpdatesStatusCountsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser("user_local", userId);
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);

        var schedules = new List<Schedule>
        {
            CreateTestSchedule("created", localUserId: userId),
            CreateTestSchedule("created", localUserId: userId),
            CreateTestSchedule("inputted", localUserId: userId),
            CreateTestSchedule("reserved", localUserId: userId),
            CreateTestSchedule("confirmed", localUserId: userId),
        };

        _mockSupabaseService.Setup(x => x.GetSchedulesAsync()).ReturnsAsync(schedules);
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        await vm.LoadSchedulesAsync();

        // Assert
        vm.AllCount.Should().Be(5);
        vm.CreatedCount.Should().Be(2);
        vm.InputtedCount.Should().Be(1);
        vm.ReservedCount.Should().Be(1);
        vm.ConfirmedCount.Should().Be(1);
    }

    #endregion

    #region 6. 삭제 테스트

    [Fact]
    public async Task ConfirmDeleteAsync_RemovesScheduleFromList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser("middle_military", userId);
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);

        var scheduleToDelete = CreateTestSchedule("created", localUserId: Guid.NewGuid());
        scheduleToDelete.CreatedBy = userId;

        _mockSupabaseService.Setup(x => x.GetSchedulesAsync())
            .ReturnsAsync(new List<Schedule> { scheduleToDelete });
        _mockSupabaseService.Setup(x => x.SoftDeleteScheduleAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);
        await vm.LoadSchedulesAsync();

        // 삭제 모달 열기
        var item = vm.Schedules.First();
        vm.DeleteScheduleCommand.Execute(item);

        // Act
        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        // Assert
        vm.Schedules.Should().BeEmpty();
        vm.AllCount.Should().Be(0);
        _mockSupabaseService.Verify(x => x.SoftDeleteScheduleAsync(scheduleToDelete.Id, userId), Times.Once);
    }

    [Fact]
    public void DeleteSchedule_NonMiddleMilitary_DoesNotShowModal()
    {
        // Arrange
        var user = CreateTestUser("user_local");
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);

        var vm = CreateViewModel(autoInitialize: false);
        var item = new ScheduleListItem
        {
            Schedule = CreateTestSchedule("created")
        };

        // Act
        vm.DeleteScheduleCommand.Execute(item);

        // Assert
        vm.ShowDeleteModal.Should().BeFalse("user_local은 삭제 권한 없음");
    }

    #endregion

    #region 7. 빈 목록 메시지 테스트

    [Fact]
    public async Task LoadSchedulesAsync_WhenEmpty_ShowsEmptyMessage()
    {
        // Arrange
        var user = CreateTestUser("user_local");
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);
        _mockSupabaseService.Setup(x => x.GetSchedulesAsync()).ReturnsAsync(new List<Schedule>());
        SetupEmptyCacheData();

        var vm = CreateViewModel(autoInitialize: false);

        // Act
        await vm.LoadSchedulesAsync();

        // Assert
        vm.ShowEmptyMessage.Should().BeTrue();
        vm.EmptyMessage.Should().Be("일정이 없습니다");
    }

    #endregion

    #region 8. 역할별 UI 설정 테스트

    [Theory]
    [InlineData("user_local", true, false, false, false)]
    [InlineData("user_military", false, true, false, false)]
    [InlineData("middle_military", false, false, true, false)]
    [InlineData("middle_local", false, false, false, true)]
    public void DetermineUserRole_SetsCorrectTabs(string role, bool localTab, bool militaryTab, bool divisionTab, bool regionTab)
    {
        // Arrange
        var user = CreateTestUser(role);
        _mockAuthService.Setup(x => x.CurrentUser).Returns(user);
        SetupEmptyCacheData();
        _mockSupabaseService.Setup(x => x.GetSchedulesAsync()).ReturnsAsync(new List<Schedule>());

        // Act - autoInitialize로 DetermineUserRole 호출
        var vm = CreateViewModel(autoInitialize: true);

        // Assert (약간의 딜레이 후 확인 - 비동기 초기화 때문)
        Task.Delay(100).Wait();

        vm.ShowLocalUserTab.Should().Be(localTab);
        vm.ShowMilitaryUserTab.Should().Be(militaryTab);
        vm.ShowDivisionTab.Should().Be(divisionTab);
        vm.ShowRegionTab.Should().Be(regionTab);
    }

    #endregion
}
