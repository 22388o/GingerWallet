using DynamicData;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models.UI;

#nullable disable

internal class NullWalletRepository : IWalletRepository
{
	public NullWalletRepository()
	{
		Wallets =
			Array.Empty<IWalletModel>()
				 .AsObservableChangeSet(x => x.Name);
	}

	public IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	public IWalletModel? DefaultWallet => null;

	public bool HasWallet => throw new NotImplementedException();

	public IWalletModel GetExistingWallet(HwiEnumerateEntry device)
	{
		throw new NotImplementedException();
	}

	public string GetNextWalletName()
	{
		return "Wallet";
	}

	public Task<IWalletSettingsModel> NewWalletAsync(WalletCreationOptions options, CancellationToken? cancelToken = null)
	{
		return Task.FromResult(default(IWalletSettingsModel));
	}

	public IWalletModel SaveWallet(IWalletSettingsModel walletSettings)
	{
		return default;
	}

	public (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName)
	{
		return null;
	}
}
