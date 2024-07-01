using CommunityToolkit.Mvvm.ComponentModel;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace OfflineAheadOfTime;

public partial class MapViewModel : ObservableObject, IDisposable
{
    #region Observable properties

    // Current map to display in the map view.
    [ObservableProperty]
    private Map _map;

    // A list of map areas to display in the UI.
    [ObservableProperty]
    private List<PreplannedMapArea> _mapAreas = new List<PreplannedMapArea>();

    // The currently selected map area.
    [ObservableProperty]
    private PreplannedMapArea _selectedMapArea;

    // Graphics overlays.
    [ObservableProperty]
    private GraphicsOverlayCollection _graphicsOverlays;

    // UI stuff.
    [ObservableProperty]
    private bool _showBusyIndicator;
    [ObservableProperty]
    private double _progressPercent;
    [ObservableProperty]
    private string _busyText;
    [ObservableProperty]
    private string _messageText;
    [ObservableProperty]
    private bool _showOnlineEnabled;
    [ObservableProperty]
    private string _displayOrDownloadButtonText = "Download";
    [ObservableProperty]
    private bool _enableDownloadOnDemand;
    #endregion
    #region Private properties

    // Portal ID of the web map that has the ahead-of-time map areas.
    private const string PortalItemId = "eca4393d40aa4facbf4fcc1f032c6d3b";

    private GraphicsOverlay _extentGraphics;

    // Task for taking map areas offline.
    private OfflineMapTask _offlineMapTask;

    // Hold onto the original (online) map.
    private Map _originalMap;

    // Most recently opened map package.
    private MobileMapPackage _mobileMapPackage;

    // Folder to store the downloaded mobile map packages.
    private string _offlineDataFolder;
    #endregion

    public MapViewModel()
    {
        _ = Init();
    }

    private async Task Init()
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await Application.Current.MainPage.DisplayAlert("Cannot access web map", "No network connection", "OK");
                return;
            }

            #region --Show a web map and available offline map areas--
            // Create a portal to enable access to the portal item.
            var portal = await ArcGISPortal.CreateAsync();

            // Create the portal item from the portal item ID.
            var webMapItem = await PortalItem.CreateAsync(portal, PortalItemId);

            // Show the web map.
            Map = new Map(webMapItem);

            // Create an offline map task for the web map item.
            _offlineMapTask = await OfflineMapTask.CreateAsync(webMapItem);

            // Get the available preplanned map areas.
            var preplannedAreas = await _offlineMapTask.GetPreplannedMapAreasAsync();

            // Load each item, then add it to a list of areas.
            var areas = new List<PreplannedMapArea>();
            foreach (PreplannedMapArea area in preplannedAreas)
            { 
                await area.LoadAsync();
                areas.Add(area);
            }

            // Set the list of preplanned map areas.
            MapAreas = areas;

            // Store a reference to the web map so it can be redisplayed (if a connection is available).
            _originalMap = Map;
            #endregion
            #region ...
            // Hide the loading indicator.
            ShowBusyIndicator = false;

            // Get the offline folder to write map packages to.
            _offlineDataFolder = GetDataFolder();

            // Set up overlays to show the map area extent graphics.
            var lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Red, 2);
            _extentGraphics = new GraphicsOverlay 
            {
                Renderer = new SimpleRenderer(new SimpleFillSymbol(SimpleFillSymbolStyle.DiagonalCross, System.Drawing.Color.White, lineSymbol))
            };
            GraphicsOverlays = new GraphicsOverlayCollection { _extentGraphics };
        }
        catch(Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error instantiating map.", ex.Message, "OK");
        }
        #endregion
    }

    partial void OnSelectedMapAreaChanged(PreplannedMapArea value)
    {
        PreplannedMapArea preplannedMap = value;
        if (preplannedMap == null) { return; }

        // Show the area of interest (extent) for this offline map area.
        _extentGraphics.Graphics.Clear();
        if (!ShowOnlineEnabled)
        {
            _extentGraphics.Graphics.Add(new Graphic(preplannedMap.AreaOfInterest));
        }

        // Allow the user to either download or display the offline map (if it's already been downloaded).
        string localMapPath = Path.Combine(_offlineDataFolder, preplannedMap.PortalItem.Title);
        if (Directory.Exists(localMapPath))
        {
            // The map is available on the device.
            DisplayOrDownloadButtonText = "Display";
        }
        else
        {
            // The map does not exist in the local directory.
            DisplayOrDownloadButtonText = "Download";
        }
    }

    public async Task DownloadOrDisplaySelectedMapAreaAsync()
    {
        #region ...
        if (SelectedMapArea == null) { return; }
        _extentGraphics.Graphics.Clear();

        // Close the current mobile package.
        _mobileMapPackage?.Close();

        // Set up UI for downloading.
        ProgressPercent = 0;
        BusyText = "Downloading map area...";
        ShowBusyIndicator = true;
        #endregion

        // Create a local folder where the map package will be downloaded.
        string localMapAreaPath = Path.Combine(_offlineDataFolder, SelectedMapArea.PortalItem.Title);

        // Has this map already been downloaded?
        var hasLocalMap = Directory.Exists(localMapAreaPath);

        #region --Open a previously downloaded map area--

        // If the area is already downloaded, open it.
        if (hasLocalMap)
        {
            try
            {
                // Open the offline map package.
                _mobileMapPackage = await MobileMapPackage.OpenAsync(localMapAreaPath);

                // Open the first map in the package.
               Map = _mobileMapPackage.Maps.First();

                #region ....
                BusyText = string.Empty;
                ShowBusyIndicator = false;
                ShowOnlineEnabled = true;
                //MessageText = "Opened offline area.";
                #endregion
                
                return;
            }
            catch (Exception e)
            {
                await Application.Current.MainPage.DisplayAlert
                    ("Couldn't open offline area. Proceeding to take area offline.", e.Message, "OK");
            }
        }
        #endregion

        #region --Download a map area to the device--

        // Create download offlineMapParameters.
        var parameters = await _offlineMapTask.CreateDefaultDownloadPreplannedOfflineMapParametersAsync(SelectedMapArea);

        // Set the update mode to synchronize updates with feature services.
        parameters.UpdateMode = PreplannedUpdateMode.SyncWithFeatureServices;
        
        // Create the download preplanned offline map job.
        var job = _offlineMapTask.DownloadPreplannedOfflineMap(parameters, localMapAreaPath);

        #region .
        job.ProgressChanged += (s, e) => 
        {
            // Because the event is raised on a background thread, the dispatcher must be used to
            // ensure that UI updates happen on the UI thread.
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update the UI with the progress.
                DownloadPreplannedOfflineMapJob downloadJob = s as DownloadPreplannedOfflineMapJob;
                ProgressPercent = downloadJob.Progress / 100.0;
                BusyText = $"Downloading map... {downloadJob.Progress}%";
            });
        };
        #endregion

        try
        {
            // Await the result of the job.
            var results = await job.GetResultAsync();

            // Set the current mobile map package.
            _mobileMapPackage = results.MobileMapPackage;

            #region Display any layer or table errors encountered
            // Handle possible errors and show them to the user.
            if (results.HasErrors)
            {
                // Accumulate all layer and table errors into a single message.
                string errors = "";

                foreach (KeyValuePair<Layer, Exception> layerError in results.LayerErrors)
                {
                    errors = $"{errors}\n{layerError.Key.Name} {layerError.Value.Message}";
                }

                foreach (KeyValuePair<FeatureTable, Exception> tableError in results.TableErrors)
                {
                    errors = $"{errors}\n{tableError.Key.TableName} {tableError.Value.Message}";
                }

                // Show the message.
                await Application.Current.MainPage.DisplayAlert("Warning!", errors, "OK");
            }
            #endregion

            // Show the downloaded map.
            Map = results.OfflineMap;

            #region .....
            // Update the UI.
            ShowOnlineEnabled = true;
            //MessageText = "Downloaded preplanned area.";
            DisplayOrDownloadButtonText = "Display";
        }
        catch (Exception ex)
        {
            // Report any errors.
            await Application.Current.MainPage.DisplayAlert("Downloading map area failed.", ex.Message, "OK");
        }
        finally
        {
            BusyText = string.Empty;
            ShowBusyIndicator = false;
        }
        #endregion
        #endregion

    }

    #region --Download a map on-demand--
    // Take a custom area offline (the current map extent, for example).
    public async Task DownloadMapOnDemand(Envelope offlineMapExtent)
    {
        // Create an offline map task using the current map.
        var offlineMapTask = await OfflineMapTask.CreateAsync(Map);

        // Create a default set of parameters for generating the offline map from the area of interest.
        var offlineMapParameters = await 
            offlineMapTask.CreateDefaultGenerateOfflineMapParametersAsync(offlineMapExtent);

        // Edit the parameters to include the basemap.
        offlineMapParameters.IncludeBasemap = true;

        // Create a local folder to store the map package download.
        string localMapPath = Path.Combine(_offlineDataFolder, "OfflineMap_" + DateTime.Now.ToFileTime().ToString());

        // Create the job to generate the offline map.
        var generateJob = offlineMapTask.GenerateOfflineMap(offlineMapParameters, localMapPath);

        // Handle the progress changed event for the job. When the download completes, the map will display.
        generateJob.ProgressChanged += DownloadOnDemandProgressChanged;
        generateJob.Start();

        ShowBusyIndicator = true;
    }

    private async void DownloadOnDemandProgressChanged(object sender, EventArgs e)
    {
        try
        {
            // Get the generate offline map job.
            var generateJob = sender as GenerateOfflineMapJob;
            if (generateJob == null) { return; }

            // If the job succeeds, show the offline map in the map view.
            if (generateJob.Status == Esri.ArcGISRuntime.Tasks.JobStatus.Succeeded)
            {
                var result = await generateJob.GetResultAsync();
                Map = result.OfflineMap;
                #region ...
                ShowBusyIndicator = false;
                #endregion
            }
            // If the job fails, notify the user.
            else if (generateJob.Status == Esri.ArcGISRuntime.Tasks.JobStatus.Failed)
            {
                await Application.Current.MainPage.DisplayAlert("Downloading map area failed.", generateJob.Error.Message, "OK");
                #region ...
                ShowBusyIndicator = false;
                #endregion
            }
            else
            {
                // Show progress in the UI.
                BusyText = $"Generate offline map: {generateJob.Progress}%";
                ProgressPercent = generateJob.Progress / 100.0;
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Downloading map area failed.", ex.Message, "OK");
        }

    }
    #endregion

    #region --Synchronize edits--
    public async Task<bool> SynchronizeMapAreaEdits()
    {
        #region ...
        // Skip if we're viewing the online map.
        if (Map == _originalMap) { return false; }
        #endregion

        // Create a task to synchronize edits with the web map.
        var offlineMapSyncTask = await OfflineMapSyncTask.CreateAsync(Map);

        // Get default synchronization parameters.
        var syncParameters = await offlineMapSyncTask.CreateDefaultOfflineMapSyncParametersAsync();

        // Update the sync direction to "download" to fetch server updates.
        syncParameters.SyncDirection = SyncDirection.Download;

        // Synchronize the map.
        var syncJob = offlineMapSyncTask.SyncOfflineMap(syncParameters);
        var syncResult = await syncJob.GetResultAsync();

        // Return a bool that indicates whether the sync had errors.
        return !syncResult.HasErrors;

        // (Can get more info about sync results and errors)
        //var layerSyncResults = syncResult.LayerResults;
        //var tableSyncResults = syncResult.TableResults;
    }
    #endregion

    #region ...
    public void ShowOnlineMap()
    {
        Map = _originalMap; 
        ShowOnlineEnabled = false;
    }

    public async Task DeleteAllOfflineMaps()
    {
        try
        {
            BusyText = "Deleting downloaded map area...";
            ShowBusyIndicator = true;

            // Reset the map.
            Map = _originalMap;

            // Close the current mobile package.
            _mobileMapPackage?.Close();

            // Delete the offline data for the selected map area.
            // Get the local folder where the map package is downloaded.
            string areaName = SelectedMapArea.PortalItem.Title;
            string localMapAreaPath = Path.Combine(_offlineDataFolder, areaName);

            // Delete the map if it exists.
            if (Directory.Exists(localMapAreaPath))
            {
                Directory.Delete(localMapAreaPath, true);
            }
            else
            {
                // Delete everything in the offline folder, then recreate it.
                Directory.Delete(_offlineDataFolder, true);
                Directory.CreateDirectory(_offlineDataFolder);
            }

            // Update the UI.
            //MessageText = $"Deleted {areaName}.";
            DisplayOrDownloadButtonText = "Download";
        }
        catch (Exception ex)
        {
            // Report the error.
            await Application.Current.MainPage.DisplayAlert("Deleting map areas failed.", ex.Message, "OK");
        }
        finally
        {
            ShowBusyIndicator = false;
            ShowOnlineEnabled = false;
        }
    }

    /// Gets the data folder where locally provisioned data is stored.
    public static string GetDataFolder()
    {
#if NETFX_CORE
            string appDataFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
#elif MACCATALYST
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#elif MAUI
            string appDataFolder = FileSystem.Current.AppDataDirectory;
#else
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#endif
        string sampleDataFolder = Path.Combine(appDataFolder, "ESRI", "dotnetSamples", "Data");

        if (!Directory.Exists(sampleDataFolder)) { Directory.CreateDirectory(sampleDataFolder); }

        return sampleDataFolder;
    }

    public void Dispose()
    {
        // Close the current mobile package.
        _mobileMapPackage?.Close();
    }
    #endregion
}
