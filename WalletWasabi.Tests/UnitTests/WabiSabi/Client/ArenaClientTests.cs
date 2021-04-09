using Moq;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class ArenaClientTests
	{
		[Fact]
		public async Task FullCoinjoinAsyncTest()
		{
			var config = new WabiSabiConfig { MaxInputCountByRound = 1 };
			var round = WabiSabiFactory.CreateRound(config);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

			using var key = new Key();
			var outpoint = BitcoinFactory.CreateOutPoint();

			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetTxOutAsync(outpoint.Hash, (int)outpoint.N, true))
				.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
				{
					IsCoinBase = false,
					Confirmations = 200,
					TxOut = new TxOut(Money.Coins(1m), key.PubKey.WitHash.GetAddress(Network.Main)),
				});
			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);

			var apiClient = new ArenaClient(round.AmountCredentialIssuerParameters, round.WeightCredentialIssuerParameters, coordinator, new InsecureRandom());
			var amountCredentials = apiClient.AmountCredentialClient.Credentials;
			var weightCredentials = apiClient.WeightCredentialClient.Credentials;

			var aliceId = await apiClient.RegisterInputAsync(Money.Coins(1m), outpoint, key, round.Id, round.Hash);

			Assert.NotEqual(Guid.Empty, aliceId);
			Assert.Empty(amountCredentials.Valuable);

			var reissuanceAmounts = new[]
			{
				Money.Coins(.75m) - round.FeeRate.GetFee(Constants.P2wpkhInputVirtualSize),
				Money.Coins(.25m)
			};

			var inputWeight = Constants.WitnessScaleFactor * Constants.P2wpkhInputVirtualSize;
			var inputRemainingWeights = new[] { (long)ArenaClient.ProtocolMaxWeightPerAlice - inputWeight };

			// Phase: Input Registration
			Assert.Equal(Phase.InputRegistration, round.Phase);

			await apiClient.ConfirmConnectionAsync(
				round.Id,
				aliceId,
				inputRemainingWeights,
				amountCredentials.ZeroValue.Take(ArenaClient.ProtocolCredentialNumber),
				reissuanceAmounts);

			Assert.Empty(amountCredentials.Valuable);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			// Phase: Connection Confirmation
			await apiClient.ConfirmConnectionAsync(
				round.Id,
				aliceId,
				inputRemainingWeights,
				amountCredentials.ZeroValue.Take(ArenaClient.ProtocolCredentialNumber),
				reissuanceAmounts);

			Assert.Single(apiClient.AmountCredentialClient.Credentials.Valuable, x => x.Amount.ToMoney() == reissuanceAmounts.First());
			Assert.Single(apiClient.AmountCredentialClient.Credentials.Valuable, x => x.Amount.ToMoney() == reissuanceAmounts.Last());

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Phase: Output Registration
			using var destinationKey1 = new Key(); 
			using var destinationKey2 = new Key(); 

			await apiClient.RegisterOutputAsync(
				round.Id, 
				reissuanceAmounts[0],
				destinationKey1.PubKey.WitHash.ScriptPubKey,
				apiClient.AmountCredentialClient.Credentials.Valuable,
				apiClient.WeightCredentialClient.Credentials.Valuable);

			await apiClient.RegisterOutputAsync(
				round.Id, 
				reissuanceAmounts[1],
				destinationKey2.PubKey.WitHash.ScriptPubKey,
				apiClient.AmountCredentialClient.Credentials.Valuable,
				apiClient.WeightCredentialClient.Credentials.Valuable);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);

			Assert.Equal(1, round.Coinjoin.Inputs.Count);
			Assert.Equal(2, round.Coinjoin.Outputs.Count);
		}

		[Fact]
		public async Task RemoveInputAsyncTest()
		{
			var config = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(config);
			round.SetPhase(Phase.ConnectionConfirmation);
			var fundingTx = BitcoinFactory.CreateSmartTransaction(ownOutputCount: 1);
			var coin = fundingTx.WalletOutputs.First().Coin;
			var alice = new Alice(new Dictionary<Coin, byte[]> { { coin, Array.Empty<byte>() } });
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);

			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, arena.Rpc);
			var apiClient = new ArenaClient(null!, null!, coordinator);

			round.SetPhase(Phase.InputRegistration);

			await apiClient.RemoveInputAsync(round.Id, alice.Id);
			Assert.Empty(round.Alices);
		}

		[Fact]
		public async Task SignTransactionAsync()
		{
			WabiSabiConfig config = new();
			Round round = WabiSabiFactory.CreateRound(config);

			using Key key1 = new();
			Alice alice1 = WabiSabiFactory.CreateAlice(key: key1);
			round.Alices.Add(alice1);

			using Key key2 = new();
			Alice alice2 = WabiSabiFactory.CreateAlice(key: key2);
			round.Alices.Add(alice2);

			var coinjoin = round.Coinjoin;
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);

			var mockRpc = new Mock<IRPCClient>();
			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);

			var rnd = new InsecureRandom();
			var amountClient = new WabiSabiClient(round.AmountCredentialIssuerParameters, 2, rnd, 4300000000000ul);
			var weightClient = new WabiSabiClient(round.WeightCredentialIssuerParameters, 2, rnd, 2000ul);
			var apiClient = new ArenaClient(amountClient, weightClient, coordinator);

			round.SetPhase(Phase.TransactionSigning);

			// No inputs in the CoinJoin.
			await Assert.ThrowsAsync<ArgumentException>(async () => await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key1, Network.Main), coinjoin));

			coinjoin.Inputs.Add(alice1.Coins.First().Outpoint);

			// Trying to sign coins those are not in the CoinJoin.
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await apiClient.SignTransactionAsync(round.Id, alice2.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), coinjoin));

			coinjoin.Inputs.Add(alice2.Coins.First().Outpoint);

			// Trying to sign coins with the wrong secret.
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), coinjoin));

			Assert.False(round.Coinjoin.HasWitness);

			await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key1, Network.Main), coinjoin);
			Assert.True(round.Coinjoin.Inputs.Where(i => alice1.Coins.Select(c => c.Outpoint).Contains(i.PrevOut)).All(i => i.HasWitScript()));

			await apiClient.SignTransactionAsync(round.Id, alice2.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), coinjoin);
			Assert.True(round.Coinjoin.Inputs.Where(i => alice2.Coins.Select(c => c.Outpoint).Contains(i.PrevOut)).All(i => i.HasWitScript()));
		}
	}
}
