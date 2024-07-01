namespace OfflineWithPackages;

public partial class PackageInfoPage : ContentPage
{
	public PackageInfoPage(MapViewModel vm)
	{
		InitializeComponent();

        this.BindingContext = vm;
    }

    private async void ShowMapButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }

}