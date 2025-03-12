using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Mapping.FeatureForms;
using Esri.ArcGISRuntime.Toolkit.Maui;

namespace OfflineAheadOfTime;

public partial class MainPage : ContentPage
{
    private MapViewModel _viewModel;
    public MainPage(MapViewModel vm)
    {
        this.SizeChanged += FeatureFormViewSample_SizeChanged;
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
        DownloadOnDemandButton.IsVisible = !_viewModel.ShowOnlineEnabled && MyMapView.MapScale < 30000;
    }

    // Feature form related code
    private async void mapView_GeoViewTapped(object sender, Esri.ArcGISRuntime.Maui.GeoViewInputEventArgs e)
    {
        try
        {
            var result = await MyMapView.IdentifyLayersAsync(e.Position, 3, false);

            // Retrieves feature from IdentifyLayerResult with a form definition
            var feature = GetFeature(result); //, out var def);
            if (feature != null)
            {
                formViewer.FeatureForm = new FeatureForm(feature);
                SidePanel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(ex.GetType().Name, ex.Message, "OK");
        }
    }

    private ArcGISFeature? GetFeature(IEnumerable<IdentifyLayerResult> results) //, out FeatureFormDefinition? def)
    {
        //def = null;
        if (results == null)
            return null;
        foreach (var result in results.Where(r => r.LayerContent is FeatureLayer layer && (layer.FeatureFormDefinition is not null || (layer.FeatureTable as ArcGISFeatureTable)?.FeatureFormDefinition is not null)))
        {
            var feature = result.GeoElements?.OfType<ArcGISFeature>()?.FirstOrDefault();
            //def = (result.LayerContent as FeatureLayer)?.FeatureFormDefinition ?? ((result.LayerContent as FeatureLayer)?.FeatureTable as ArcGISFeatureTable)?.FeatureFormDefinition;
            if (feature != null) // && def != null)
            {
                return feature;
            }
        }

        return null;
    }
    private void DiscardButton_Click(object sender, EventArgs e)
    {
        formViewer.FeatureForm.DiscardEdits();
    }
    private void CloseButton_Click(object sender, EventArgs e)
    {
        formViewer.FeatureForm = null;
        SidePanel.IsVisible = false;
    }
    private async void UpdateButton_Click(object sender, EventArgs e)
    {
        if (formViewer.FeatureForm == null) return;
        if (!formViewer.IsValid)
        {
            var errorsMessages = formViewer.FeatureForm.Elements.OfType<FieldFormElement>().Where(e => e.ValidationErrors.Any()).Select(s => s.FieldName + ": " + string.Join(",", s.ValidationErrors.Select(e => e.Message)));
            if (errorsMessages.Any())
            {
                await DisplayAlert("Form has errors", string.Join("\n", errorsMessages), "OK");
                return;
            }
        }
        try
        {
            await formViewer.FinishEditingAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to apply edits:\n" + ex.Message, "OK");
        }
    }
    private void FeatureFormViewSample_SizeChanged(object? sender, EventArgs e)
    {
        // Programmatic adaptive layout
        // Consider using AdaptiveTriggers instead once they work predictably
        if (this.Width > 500)
        {
            // Use side panel
            Grid.SetColumnSpan(MyMapView, 1);
            //Grid.SetColumn(SidePanel, 1);
            SidePanel.WidthRequest = 300;
        }
        else
        {
            // Full screen panel
            Grid.SetColumnSpan(MyMapView, 2);
            Grid.SetColumn(SidePanel, 0);
            SidePanel.WidthRequest = -1;
        }
    }
}