namespace OfflineWithPackages;

public partial class ScenePage : ContentPage
{
	public ScenePage(MapViewModel vm)
	{
		InitializeComponent();

		this.BindingContext = vm;
	}
}