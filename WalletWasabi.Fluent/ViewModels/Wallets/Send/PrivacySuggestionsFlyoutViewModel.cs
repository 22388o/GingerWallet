using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PrivacySuggestionsFlyoutViewModel : ViewModelBase
{
	[AutoNotify] private SuggestionViewModel? _previewSuggestion;
	[AutoNotify] private SuggestionViewModel? _selectedSuggestion;
	[AutoNotify] private bool _isOpen;

	public PrivacySuggestionsFlyoutViewModel()
	{
		Suggestions = new ObservableCollection<SuggestionViewModel>();

		this.WhenAnyValue(x => x.IsOpen)
			.Subscribe(x =>
			{
				if (!x)
				{
					PreviewSuggestion = null;
				}
			});
	}

	public ObservableCollection<SuggestionViewModel> Suggestions { get; }

	public async Task BuildPrivacySuggestionsAsync(Wallet wallet, TransactionInfo info, BitcoinAddress destination, BuildTransactionResult transaction, bool isFixedAmount, CancellationToken cancellationToken)
	{
		using CancellationTokenSource childCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		childCTS.CancelAfter(15000);

		Suggestions.Clear();
		SelectedSuggestion = null;

		if (!info.IsPrivate)
		{
			Suggestions.Add(new PocketSuggestionViewModel(SmartLabel.Merge(transaction.SpentCoins.Select(x => CoinHelpers.GetLabels(x)))));
		}

		var loadingRing = new LoadingSuggestionViewModel();
		Suggestions.Add(loadingRing);

		try
		{
			var hasChange = transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != destination.ScriptPubKey);

			if (hasChange && !isFixedAmount)
			{
				var suggestions =
					ChangeAvoidanceSuggestionViewModel.GenerateSuggestionsAsync(info, destination, wallet, childCTS.Token);

				await foreach (var suggestion in suggestions)
				{
					Suggestions.Insert(Suggestions.Count - 1, suggestion);
				}
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogWarning("Computing privacy suggestions timed out.");
		}
		finally
		{
			Suggestions.Remove(loadingRing);
		}
	}
}
