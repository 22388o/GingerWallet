using System.Collections.ObjectModel;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class LocationWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	[AutoNotify] private ObservableCollection<string> _countries;
	[AutoNotify] private ObservableCollection<string> _country;

	public LocationWorkflowInputValidatorViewModel(IWorkflowValidator workflowValidator)
		: base(workflowValidator, null, "Enter your location...")
	{
		// TODO: Get from service.
		_countries = new ObservableCollection<string>()
		{
			"Austria",
			"Belgium",
			"Bulgaria",
			"Croatia",
			"Cyprus",
			"Czech Republic",
			"Denmark",
			"Estonia",
			"Finland",
			"France",
			"Germany",
			"Greece",
			"Hungary",
			"Ireland",
			"Italy",
			"Latvia",
			"Lithuania",
			"Luxembourg",
			"Malta",
			"Netherlands",
			"Poland",
			"Portugal",
			"Romania",
			"Slovakia",
			"Slovenia",
			"Spain",
			"Sweden",
			"Canada",
			"Switzerland",
			"United Kingdom",
			"United States of America",
		};
		_country = new ObservableCollection<string>();

		this.WhenAnyValue(x => x.Country.Count)
			.Subscribe(_ => WorkflowValidator.Signal(IsValid()));
	}

	public override bool IsValid()
	{
		// TODO: Validate location.
		return _country.Count == 1 && !string.IsNullOrWhiteSpace(_country[0]);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			return _country[0];
		}

		return null;
	}
}
