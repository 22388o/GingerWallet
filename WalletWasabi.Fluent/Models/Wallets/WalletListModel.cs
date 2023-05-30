using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletListModel : ReactiveObject, IWalletListModel
{
	public WalletListModel()
	{
		// Convert the Wallet Manager's contents into an observable stream of IWalletModels.
		Wallets =
			Observable.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded)).Select(_ => Unit.Default)
				      .StartWith(Unit.Default)
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .SelectMany(_ => Services.WalletManager.GetWallets())
					  .ToObservableChangeSet(x => x.WalletName)
					  .Transform(wallet => new WalletModel(wallet))

					  // Refresh the collection when logged in.
					  .AutoRefresh(x => x.IsLoggedIn)

					  // Sort the list to put the most recently logged in wallet to the top.
					  .Sort(SortExpressionComparer<IWalletModel>.Descending(i => i.IsLoggedIn).ThenByAscending(x => x.Name))
					  .Transform(x => x as IWalletModel);

		// Materialize the Wallet list to determine the default wallet.
		Wallets
			.Bind(out var wallets)
			.Subscribe();

		DefaultWallet =
			wallets.FirstOrDefault(item => item.Name == Services.UiConfig.LastSelectedWallet)
			?? wallets.FirstOrDefault();
	}

	public IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	public IWalletModel? DefaultWallet { get; }
}
