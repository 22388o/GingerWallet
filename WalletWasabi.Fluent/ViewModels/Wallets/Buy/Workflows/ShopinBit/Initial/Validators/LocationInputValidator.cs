using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class LocationInputValidator : InputValidator
{
	private readonly InitialWorkflowRequest _initialWorkflowRequest;
	private Country[] _countriesSource;

	[AutoNotify] private ObservableCollection<string> _countries;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<string> _country;

	public LocationInputValidator(
		WorkflowState workflowState,
		Country[] countries,
		InitialWorkflowRequest initialWorkflowRequest,
		ChatMessageMetaData.ChatMessageTag tag)
		: base(workflowState, null, "Enter your location...", "Next", tag)
	{
		_initialWorkflowRequest = initialWorkflowRequest;
		_countriesSource = countries;
		_countries = new ObservableCollection<string>(_countriesSource.Select(x => x.Name));
		_country = new ObservableCollection<string>();

		this.WhenAnyValue(x => x.Country.Count)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
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
			var location = _countriesSource[_countries.IndexOf(_country[0])];

			_initialWorkflowRequest.Location = location;

			return _country[0];
		}

		return null;
	}
}
