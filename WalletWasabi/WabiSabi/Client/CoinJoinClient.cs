using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinJoinClient : BackgroundService, IDisposable
	{
		private bool _disposedValue;

		public CoinJoinClient(
			IWabiSabiApiRequestHandler arenaRequestHandler,
			IEnumerable<Coin> coins,
			Kitchen kitchen,
			KeyManager keymanager,
			RoundStateUpdater roundStatusUpdater)
		{
			ArenaRequestHandler = arenaRequestHandler;
			Kitchen = kitchen;
			Keymanager = keymanager;
			RoundStatusUpdater = roundStatusUpdater;
			SecureRandom = new SecureRandom();
			Coins = coins;
		}

		private ZeroCredentialPool ZeroAmountCredentialPool { get; } = new();
		private ZeroCredentialPool ZeroVsizeCredentialPool { get; } = new();
		private SecureRandom SecureRandom { get; }
		private CancellationTokenSource DisposeCts { get; } = new();
		private IEnumerable<Coin> Coins { get; set; }
		private Random Random { get; } = new();
		public IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }
		private RoundStateUpdater RoundStatusUpdater { get; }

		protected override async Task ExecuteAsync(CancellationToken cancellationToken)
		{
			try
			{
				var roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, cancellationToken).ConfigureAwait(false);
				var constructionState = roundState.Assert<ConstructionState>();

				// Calculate outputs values
				var outputValues = DecomposeAmounts(roundState);

				// Get all locked internal keys we have and assert we have enough.
				Keymanager.AssertLockedInternalKeysIndexed(howMany: Coins.Count());
				var allLockedInternalKeys = Keymanager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked);
				var outputs = outputValues.Zip(allLockedInternalKeys, (amount, hdPubKey) => new TxOut(amount, hdPubKey.P2wpkhScript));

				var plan = CreatePlan(
					Coins.Select(x => (ulong)x.Amount.Satoshi),
					Coins.Select(x => (ulong)x.ScriptPubKey.EstimateInputVsize()),
					outputValues);

				var aliceClients = CreateAliceClients(roundState);

				// Register coins.
				aliceClients = await RegisterCoinsAsync(aliceClients, roundState, cancellationToken).ConfigureAwait(false);

				// Confirm coins.
				aliceClients = await ConfirmConnectionsAsync(aliceClients, roundState, cancellationToken).ConfigureAwait(false);

				// Output registration.
				roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
				var outputsWithCredentials = outputs.Zip(aliceClients, (output, alice) => (output, alice.RealAmountCredentials, alice.RealVsizeCredentials));
				await RegisterOutputsAsync(outputsWithCredentials, roundState, cancellationToken).ConfigureAwait(false);

				// Signing.
				roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
				var signingState = roundState.Assert<SigningState>();
				var unsignedCoinJoin = signingState.CreateUnsignedTransaction();

				// Sanity check.
				SanityCheck(outputs, unsignedCoinJoin, roundState, cancellationToken);

				// Send signature.
				await SignTransactionAsync(aliceClients, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// The game is over for this round, no fallback mechanism. In the next round we will create another CoinJoinClient and try again.
			}
		}

		private List<AliceClient> CreateAliceClients(RoundState roundState)
		{
			List<AliceClient> aliceClients = new();
			foreach (var coin in Coins)
			{
				var aliceArenaClient = new ArenaClient(
					roundState.AmountCredentialIssuerParameters,
					roundState.VsizeCredentialIssuerParameters,
					ZeroAmountCredentialPool,
					ZeroVsizeCredentialPool,
					ArenaRequestHandler,
					SecureRandom);

				var hdKey = Keymanager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey).Single();
				var secret = hdKey.PrivateKey.GetBitcoinSecret(Keymanager.GetNetwork());
				aliceClients.Add(new AliceClient(roundState.Id, aliceArenaClient, coin, roundState.FeeRate, secret));
			}
			return aliceClients;
		}

		private async Task<List<AliceClient>> RegisterCoinsAsync(IEnumerable<AliceClient> aliceClients, RoundState roundState, CancellationToken cancellationToken)
		{
			var registerRequests = aliceClients.Select(alice => WrapCall(alice, alice.RegisterInputAsync(cancellationToken)));
			var completedRequests = await Task.WhenAll(registerRequests).ConfigureAwait(false);

			foreach (var request in completedRequests.Where(x => !x.Success))
			{
				Logger.LogWarning($"Round ({roundState.Id}), Alice ({request.Sender.AliceId}): {nameof(AliceClient.RegisterInputAsync)} failed, reason:'{request.Exception}'.");
			}
			return completedRequests.Where(x => x.Success).Select(x => x.Sender).ToList();
		}

		private async Task<List<AliceClient>> ConfirmConnectionsAsync(IEnumerable<AliceClient> aliceClients, RoundState roundState, CancellationToken cancellationToken)
		{
			var confirmationRequests = aliceClients.Select(alice => WrapCall(alice, alice.ConfirmConnectionAsync(TimeSpan.FromMilliseconds(Random.Next(100, 1_000)), cancellationToken))).ToArray();
			var completedRequests = await Task.WhenAll(confirmationRequests).ConfigureAwait(false);

			foreach (var request in completedRequests.Where(x => !x.Success))
			{
				Logger.LogWarning($"Round ({roundState.Id}), Alice ({request.Sender.AliceId}): {nameof(AliceClient.ConfirmConnectionAsync)} failed, reason:'{request.Exception}'.");
			}

			return completedRequests.Where(x => x.Success).Select(x => x.Sender).ToList();
		}

		private IEnumerable<Money> DecomposeAmounts(RoundState roundState)
		{
			return Coins.Select(c => c.Amount - roundState.FeeRate.GetFee(c.ScriptPubKey.EstimateInputVsize()));
		}

		private IEnumerable<IEnumerable<(ulong RealAmountCredentialValue, ulong RealVsizeCredentialValue, Money Value)>> CreatePlan(
			IEnumerable<ulong> realAmountCredentialValues,
			IEnumerable<ulong> realVsizeCredentialValues,
			IEnumerable<Money> outputValues)
		{
			yield return realAmountCredentialValues.Zip(realVsizeCredentialValues, outputValues, (a, v, o) => (a, v, o));
		}

		private async Task RegisterOutputsAsync(
			IEnumerable<(TxOut Output, Credential[] RealAmountCredentials, Credential[] RealVsizeCredentials)> outputsWithCredentials,
			RoundState roundState,
			CancellationToken cancellationToken)
		{
			var bobClients = Enumerable.Range(0, int.MaxValue).Select(_ => CreateBobClient(roundState));
			var outputRegistrationData = outputsWithCredentials.Zip(
					bobClients,
					(o, b) => (
						TxOut: o.Output,
						RealAmountCredentials: o.RealAmountCredentials,
						RealVsizeCredentials: o.RealVsizeCredentials,
						BobClient: b));

			var outputRegisterRequests = outputRegistrationData
				.Select(x => WrapCall(x.TxOut, x.BobClient.RegisterOutputAsync(x.TxOut.Value, x.TxOut.ScriptPubKey, x.RealAmountCredentials, x.RealVsizeCredentials, cancellationToken)));
			var completedRequests = await Task.WhenAll(outputRegisterRequests).ConfigureAwait(false);

			foreach (var request in completedRequests.Where(x => !x.Success))
			{
				Logger.LogWarning($"Round ({roundState.Id}), Bob ({request.Sender.ScriptPubKey}): {nameof(BobClient.RegisterOutputAsync)} failed, reason:'{request.Exception}'.");
			}
		}

		private BobClient CreateBobClient(RoundState roundState)
		{
			return new BobClient(
				roundState.Id,
				new(
					roundState.AmountCredentialIssuerParameters,
					roundState.VsizeCredentialIssuerParameters,
					ZeroAmountCredentialPool,
					ZeroVsizeCredentialPool,
					ArenaRequestHandler,
					SecureRandom));
		}

		public async override Task StartAsync(CancellationToken cancellationToken)
		{
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(DisposeCts.Token, cancellationToken);
			await base.StartAsync(linkedCts.Token).ConfigureAwait(false);
		}

		private void SanityCheck(IEnumerable<TxOut> outputs, Transaction unsignedCoinJoinTransaction, RoundState roundState, CancellationToken cancellationToken)
		{
			var coinJoinOutputs = unsignedCoinJoinTransaction.Outputs.Select(o => (o.Value, o.ScriptPubKey));
			var expectedOutputs = outputs.Select(o => (o.Value, o.ScriptPubKey));
			if (coinJoinOutputs.IsSuperSetOf(expectedOutputs))
			{
				throw new InvalidOperationException($"Round ({roundState.Id}): My output is missing.");
			}
		}

		private async Task SignTransactionAsync(IEnumerable<AliceClient> aliceClients, Transaction unsignedCoinJoinTransaction, CancellationToken cancellationToken)
		{
			foreach (var aliceClient in aliceClients)
			{
				await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, cancellationToken).ConfigureAwait(false);
			}
		}

		private async Task<(bool Success, TSender Sender, Exception? Exception)> WrapCall<TSender>(TSender sender, Task task)
		{
			try
			{
				await task.ConfigureAwait(false);
				return (true, sender, default);
			}
			catch (Exception e)
			{
				return (false, sender, e);
			}
		}

		public async override Task StopAsync(CancellationToken cancellationToken)
		{
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(DisposeCts.Token, cancellationToken);
			await base.StopAsync(linkedCts.Token).ConfigureAwait(false);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					DisposeCts.Cancel();
					SecureRandom.Dispose();
				}
				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
