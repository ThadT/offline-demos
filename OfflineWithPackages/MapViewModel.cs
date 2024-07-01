using CommunityToolkit.Mvvm.ComponentModel;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;
using Esri.ArcGISRuntime.UI;
using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace OfflineWithPackages;

/// <summary>
/// Provides map data to an application
/// </summary>
public partial class MapViewModel : ObservableObject
{
    public MapViewModel()
    {
        Init();
    }

    #region Properties
    [ObservableProperty]
    private List<PackageInfo> _availableMapPackages;
    [ObservableProperty]
    private PackageInfo _selectedMapPackage;
    [ObservableProperty]
    private GraphicsOverlayCollection _graphicsOverlays;
    [ObservableProperty]
    private Scene _scene;
    #endregion

    partial void OnSelectedMapPackageChanged(PackageInfo value)
    {
        // Load the selected map package to get info about it (maps, locators, networks, etc).
        _ = value.LoadPackage();
    }

    private void Init()
    {
        // Create the list of available map packages (.mmpk files), stored as app assets.
        // PackageInfo is a custom class that describes a map package
        AvailableMapPackages = [
            new PackageInfo("Palm Springs", "PalmSprings_DayNight.mmpk"),
            new PackageInfo("Palm Springs CC", "PSCC_MMPK_With_Network.mmpk"),
            new PackageInfo("San Francisco", "SanFrancisco.mmpk"),
            new PackageInfo("Yellowstone", "Yellowstone.mmpk"),
            new PackageInfo("LA StreetMap Premium", "Greater_Los_Angeles.mmpk")
        ];

        #region ...
        ReadScenePackage();
        SetupGraphicsOverlays();
        #endregion
    }

    private async void ReadScenePackage()
    {
        #region ...
        //var scenePackageFile = await PackageInfo.WriteAssetToAppDataDirectoryFile("Assets/Philadelphia.mspk", "Philadelphia.mspk");
        #endregion
        
        //var scenePackage = new MobileScenePackage(scenePackageFile);
        //await scenePackage.LoadAsync();
        //Scene = scenePackage.Scenes.FirstOrDefault();
    }

    #region ...
    // internally used variables.
    private GraphicsOverlay _routeStopsOverlay;
    private GraphicsOverlay _routePathOverlay;
    private GraphicsOverlay _geocodePointsOverlay;
    private MapPoint _startPoint;
    private MapPoint _endPoint;

    private void SetupGraphicsOverlays()
    {
        // Create graphics overlays for showing some basic interaction with the maps (geocode, routing).
        _routePathOverlay = new GraphicsOverlay
        {
            Renderer = new SimpleRenderer(
                new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Cyan, 2)
                )
        };
        _routeStopsOverlay = new GraphicsOverlay
        {
            Renderer = new SimpleRenderer(
                new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Pink, 14)
                )
        };

        _geocodePointsOverlay = new GraphicsOverlay
        {
            Renderer = new SimpleRenderer(
                new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.DarkRed, 14)
                )
        };

        GraphicsOverlays =
        [
            _routeStopsOverlay,
            _geocodePointsOverlay,
            _routePathOverlay,
        ];
    }

    private async Task<string> GetReverseGeocodeAddress(MapPoint tappedPoint)
    {
        var packageLocator = SelectedMapPackage?.MapPackage?.LocatorTask;
        if (packageLocator == null) { return null; }

        var address = "";
        try
        {
            // Reverse geocode to get an address.
            IReadOnlyList<GeocodeResult> results = await packageLocator.ReverseGeocodeAsync(tappedPoint);

            // Get the display address.
            address = results[0]?.Label;
        }catch(Exception)
        {
            //TODO: handle exception
        }

        // Return the address.
        return address;
    }

    public async Task<string> HandleMapViewTapped(MapPoint tappedPoint)
    {
        // Clear any existing route paths.
        _routePathOverlay.Graphics.Clear();

        // Check if there is a network available for routing.
        var networkDataset = SelectedMapPackage?.SelectedMap?.TransportationNetworks?.FirstOrDefault();
        if (networkDataset == null) {
            _routeStopsOverlay.Graphics.Clear();
            // If not, try to use a locator (get an address at the tap point).
            var address = await GetReverseGeocodeAddress(tappedPoint);
            return address; // an address string if found by a locator, empty string (or null) otherwise.
        }
        
        // Set the start point if it hasn't been set.
        if (_startPoint == null)
        {
            _routeStopsOverlay.Graphics.Clear();

            _startPoint = tappedPoint;

            // Show the start point.
            _routeStopsOverlay.Graphics.Add(new Graphic(_startPoint));

            return null;
        }

        if (_endPoint == null)
        {
            // Show the end point.
            _endPoint = tappedPoint;
            _routeStopsOverlay.Graphics.Add(new Graphic(_endPoint));

            try
            {
                // Create the route task from the local network dataset.
                RouteTask routingTask = await RouteTask.CreateAsync(networkDataset);

                // Configure route parameters for the route between the two tapped points.
                RouteParameters routingParameters = await routingTask.CreateDefaultParametersAsync();
                List<Stop> stops = new List<Stop> { new Stop(_startPoint), new Stop(_endPoint) };
                routingParameters.SetStops(stops);

                // Get the first route result.
                RouteResult result = await routingTask.SolveRouteAsync(routingParameters);
                Route firstRoute = result.Routes[0];

                // Show the route on the map. Note that symbology for the graphics overlay is defined in Initialize().
                Polyline routeLine = firstRoute.RouteGeometry;
                _routePathOverlay.Graphics.Add(new Graphic(routeLine));
            }catch(Exception)
            {
                //TODO: handle exception
            }

            // Reset the points.
            _startPoint = null;
            _endPoint = null;
        }

        return null;
    }
    #endregion
}

public partial class PackageInfo : ObservableObject
{
    #region Properties
    [ObservableProperty]
    private string _name;
    [ObservableProperty]
    private string _packageName;
    [ObservableProperty]
    private MobileMapPackage _mapPackage;
    [ObservableProperty]
    private IReadOnlyList<Map> _maps;
    [ObservableProperty]
    private Map _selectedMap;
    [ObservableProperty]
    private List<string> _mapIndices;
    [ObservableProperty]
    private string _selectedMapIndex = "Map 0";
    [ObservableProperty]
    private bool _hasLocator;
    [ObservableProperty]
    private bool _hasStreetNetwork;
    [ObservableProperty]
    private bool _hasUtilityNetwork;
    [ObservableProperty]
    private string _expirationDateString;
    [ObservableProperty]
    private IReadOnlyList<Layer> _onlineLayers;
    [ObservableProperty]
    private IReadOnlyList<Layer> _offlineLayers;
    #endregion

    public PackageInfo(string name, string packageName)
    {
        Name = name;
        PackageName = packageName;
    }

    public async Task LoadPackage()
    {
        #region ...
        // Write bundled file stream to the AppDataDirectory so it can be referenced by the constructor.
        string pathToMapPackage = await WriteAssetToAppDataDirectoryFile($"Assets/{PackageName}", PackageName);
        #endregion
        
        // Instantiate a new mobile map package.
        MapPackage = new MobileMapPackage(pathToMapPackage);

        // Load the mobile map package.
        await MapPackage.LoadAsync();

        // See if there's a locator.
        HasLocator = MapPackage.LocatorTask != null;

        // Get the expiration date (if any).
        if (MapPackage.Expiration != null)
        {
            ExpirationDateString = MapPackage.Expiration.DateTime.ToString("MMMM dd, yyyy");
        } 
        else
        {
            ExpirationDateString = "none";
        }

        // Get the maps in the package.
        Maps = MapPackage.Maps;

        #region ...
        var mapCount = Maps.Count;
        var indices = new List<string>();
        for (int i = 1; i <= mapCount; i++)
        {
            indices.Add("Map " + i.ToString());
        }
        SelectedMapIndex = "Map -1";
        MapIndices = indices;
        #endregion
    }

    partial void OnSelectedMapIndexChanged(string value)
    {
        #region ...
        if (string.IsNullOrEmpty(value)) { return; }
        var intString = value.Split(' ')[1];
        var ok = int.TryParse(intString, out var index);
        if (!ok || index < 0) { return; }
        index--;
        #endregion

        // Get a map from the mobile map package's collection of maps.
        SelectedMap = MapPackage.Maps[index];
        SelectedMap.LoadAsync();

        // See if this map has a street network.
        HasStreetNetwork = SelectedMap.TransportationNetworks.Count > 0;

        // Check for utility networks in the map.
        HasUtilityNetwork = SelectedMap.UtilityNetworks.Count > 0;
    }

    #region ...
    public static async Task<string> WriteAssetToAppDataDirectoryFile(string resourceFilePath, string fileName)
    {
        // Check to see if the mobile map package is already present in the AppDataDirectory.
        if (File.Exists(Path.Combine(FileSystem.Current.AppDataDirectory, fileName)))
        {
            // If the mobile map package is present in the AppDataDirectory, return the file path.
            return Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
        }

        // Open the source file.
        using Stream inputStream = await FileSystem.OpenAppPackageFileAsync(resourceFilePath);

        // Create an output filename
        string targetFile = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);

        // Copy the file to the AppDataDirectory
        using FileStream outputStream = File.Create(targetFile);
        await inputStream.CopyToAsync(outputStream);

        return targetFile;
    }
    #endregion
}
