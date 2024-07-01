using Esri.ArcGISRuntime.UI;

namespace OfflineWithPackages;

public partial class MainPage : ContentPage
{
    public MainPage(MapViewModel vm)
    {
        InitializeComponent();

        this.BindingContext = vm;
    }

    private async void mapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
    {
        mapView.DismissCallout();

        var vm = this.BindingContext as MapViewModel;
        var displayText = await vm.HandleMapViewTapped(e.Location);
        if (!string.IsNullOrEmpty(displayText))
        {
            var calloutDef = new CalloutDefinition(displayText);
            mapView.ShowCalloutAt(e.Location, calloutDef);
        }
    }
}

