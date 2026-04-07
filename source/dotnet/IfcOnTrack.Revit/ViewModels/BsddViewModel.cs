// Purpose: ViewModel for bSDD Search window
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Core.License;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.ViewModels;

/// <summary>
/// ViewModel for the bSDD Search window.
/// </summary>
public partial class BsddViewModel : ObservableObject
{
    private readonly ILogger<BsddViewModel> _logger;
    private readonly LicenseManager _licenseManager;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private LicenseStatus? _licenseStatus;

    public BsddViewModel(ILogger<BsddViewModel> logger, LicenseManager licenseManager)
    {
        _logger = logger;
        _licenseManager = licenseManager;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading...";

        try
        {
            LicenseStatus = await _licenseManager.GetLicenseStatus();
            _logger.LogInformation("License status: {Type}, Valid: {IsValid}", 
                LicenseStatus.Type, LicenseStatus.IsValid);
            
            StatusMessage = $"License: {LicenseStatus.Type}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
