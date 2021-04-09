using System;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	public class HistoryItemViewModel
	{
		public HistoryItemViewModel(TransactionSummary transactionSummary, BitcoinStore bitcoinStore)
		{
			Date = transactionSummary.DateTime.ToLocalTime();
			IsCoinJoin = transactionSummary.IsLikelyCoinJoinOutput;

			var confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int) bitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = confirmations > 0;

			var amount = transactionSummary.Amount;
			if (amount < 0)
			{
				OutgoingAmount = (amount * -1).ToString(fplus: false);
			}
			else
			{
				IncomingAmount = amount.ToString(fplus: false);
			}
		}

		public DateTimeOffset Date { get; set; }

		public bool IsConfirmed { get; }

		public string? IncomingAmount { get; }

		public string? OutgoingAmount { get; }

		public bool IsCoinJoin { get; }
	}
}
