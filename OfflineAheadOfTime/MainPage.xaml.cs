namespace OfflineAheadOfTime;

public partial class MainPage : ContentPage
{
    private MapViewModel _viewModel;
    public MainPage(MapViewModel vm)
    {
        InitializeComponent();

        this.BindingContext = vm;
        _viewModel = vm;
    }

    private void OnDownloadMapAreaClicked(object sender, EventArgs e)
    {
        _ = _viewModel.DownloadOrDisplaySelectedMapAreaAsync();
    }

    private void OnDeleteAllMapAreasClicked(object sender, EventArgs e)
    {
        _ = _viewModel.DeleteAllOfflineMaps();
    }

    private void ShowOnlineButton_Clicked(object sender, EventArgs e)
    {
        _viewModel.ShowOnlineMap();
    }

    private async void OnSyncClicked(object sender, EventArgs e)
    {
        // Only try to sync if an internet connection is available.
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            // Connection to internet is available
            _ = _viewModel.SynchronizeMapAreaEdits();
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Sync edits", "No network connection", "OK");
        }
    }

    private void OnDownloadOnDemandClicked(object sender, EventArgs e)
    {
        var currentExtent = MyMapView.GetCurrentViewpoint(Esri.ArcGISRuntime.Mapping.ViewpointType.BoundingGeometry).TargetGeometry.Extent;
        _ = _viewModel.DownloadMapOnDemand(currentExtent);
    }

    private void MyMapView_ViewpointChanged(object sender, EventArgs e)
    {
        DownloadOnDemandButton.IsVisible = MyMapView.MapScale < 3000;
    }
}

