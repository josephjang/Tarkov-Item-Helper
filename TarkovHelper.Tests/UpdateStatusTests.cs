using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Pure helpers behind the update UI (top-bar redesign): the version formatting
/// shared by the title-bar chip and the Settings section, and the status-kind
/// mapping that keeps a failed re-check visible while an update found by an
/// earlier successful check remains installable.
/// </summary>
public class UpdateStatusTests
{
    [Theory]
    [InlineData("2026.7.0", "v2026.7.0")]
    [InlineData("2026.7.0.4", "v2026.7.0")] // 4-part assembly version trims to 3
    [InlineData("2026.8", "v2026.8")]       // 2-part version must not throw (ToString(3) would)
    public void FormatVersion_renders_three_parts_when_available(string input, string expected)
        => Assert.Equal(expected, UpdateService.FormatVersion(Version.Parse(input)));

    [Fact]
    public void No_completed_check_yet_is_none()
        => Assert.Equal(UpdateStatusKind.None,
            UpdateService.GetStatusKind(isChecking: false, lastCheckFailed: false, updateAvailable: false, hasCompletedCheck: false));

    [Fact]
    public void Checking_wins_over_every_other_state()
    {
        Assert.Equal(UpdateStatusKind.Checking,
            UpdateService.GetStatusKind(isChecking: true, lastCheckFailed: false, updateAvailable: false, hasCompletedCheck: false));
        Assert.Equal(UpdateStatusKind.Checking,
            UpdateService.GetStatusKind(isChecking: true, lastCheckFailed: true, updateAvailable: true, hasCompletedCheck: true));
    }

    [Fact]
    public void Failed_recheck_stays_visible_even_when_an_update_is_known()
        => Assert.Equal(UpdateStatusKind.Failed,
            UpdateService.GetStatusKind(isChecking: false, lastCheckFailed: true, updateAvailable: true, hasCompletedCheck: true));

    [Fact]
    public void Update_available_after_a_successful_check()
        => Assert.Equal(UpdateStatusKind.UpdateAvailable,
            UpdateService.GetStatusKind(isChecking: false, lastCheckFailed: false, updateAvailable: true, hasCompletedCheck: true));

    [Fact]
    public void Up_to_date_after_a_successful_check()
        => Assert.Equal(UpdateStatusKind.UpToDate,
            UpdateService.GetStatusKind(isChecking: false, lastCheckFailed: false, updateAvailable: false, hasCompletedCheck: true));
}
