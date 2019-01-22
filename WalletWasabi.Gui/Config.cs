﻿using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.TorSocks5;
using WalletWasabi.Models;

namespace WalletWasabi.Gui
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config : IConfig
	{
		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; internal set; }

		[JsonProperty(PropertyName = "MainNetBackendUriV3")]
		public string MainNetBackendUriV3 { get; private set; }

		[JsonProperty(PropertyName = "TestNetBackendUriV3")]
		public string TestNetBackendUriV3 { get; private set; }

		[JsonProperty(PropertyName = "MainNetFallbackBackendUri")]
		public string MainNetFallbackBackendUri { get; private set; }

		[JsonProperty(PropertyName = "TestNetFallbackBackendUri")]
		public string TestNetFallbackBackendUri { get; private set; }

		[JsonProperty(PropertyName = "RegTestBackendUriV3")]
		public string RegTestBackendUriV3 { get; private set; }

		[JsonProperty(PropertyName = "TorHost")]
		public string TorHost { get; internal set; }

		[JsonProperty(PropertyName = "TorSocks5Port")]
		public int? TorSocks5Port { get; internal set; }

		[JsonProperty(PropertyName = "BitcoinCoreDataDir")]
		public string BitcoinCoreDataDir
		{
			get => _bitcoinCoreDataDir;
			internal set
			{
				if (_bitcoinCoreDataDir != value)
				{
					_bitcoinCoreDataDir = value;

					if (!string.IsNullOrWhiteSpace(value) && ServiceConfiguration != default)
					{
						ServiceConfiguration.BitcoinCoreDataDir = value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "MixUntilAnonymitySet")]
		public int? MixUntilAnonymitySet
		{
			get => _mixUntilAnonymitySet;
			internal set
			{
				if (_mixUntilAnonymitySet != value)
				{
					_mixUntilAnonymitySet = value;
					if (value.HasValue && ServiceConfiguration != default)
					{
						ServiceConfiguration.MixUntilAnonymitySet = value.Value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "PrivacyLevelSome")]
		public int? PrivacyLevelSome
		{
			get => _privacyLevelSome;
			internal set
			{
				if (_privacyLevelSome != value)
				{
					_privacyLevelSome = value;
					if (value.HasValue && ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelSome = value.Value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "PrivacyLevelFine")]
		public int? PrivacyLevelFine
		{
			get => _privacyLevelFine;
			internal set
			{
				if (_privacyLevelFine != value)
				{
					_privacyLevelFine = value;
					if (value.HasValue && ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelFine = value.Value;
					}
				}
			}
		}

		[JsonProperty(PropertyName = "PrivacyLevelStrong")]
		public int? PrivacyLevelStrong
		{
			get => _privacyLevelStrong;
			internal set
			{
				if (_privacyLevelStrong != value)
				{
					_privacyLevelStrong = value;
					if (value.HasValue && ServiceConfiguration != default)
					{
						ServiceConfiguration.PrivacyLevelStrong = value.Value;
					}
				}
			}
		}

		private Uri _backendUri;
		private Uri _fallbackBackendUri;

		public ServiceConfiguration ServiceConfiguration { get; private set; }

		public Uri GetCurrentBackendUri()
		{
			if (TorProcessManager.RequestFallbackAddressUsage)
			{
				return GetFallbackBackendUri();
			}

			if (_backendUri != null) return _backendUri;

			if (Network == Network.Main)
			{
				_backendUri = new Uri(MainNetBackendUriV3);
			}
			else if (Network == Network.TestNet)
			{
				_backendUri = new Uri(TestNetBackendUriV3);
			}
			else // RegTest
			{
				_backendUri = new Uri(RegTestBackendUriV3);
			}

			return _backendUri;
		}

		public Uri GetFallbackBackendUri()
		{
			if (_fallbackBackendUri != null) return _fallbackBackendUri;

			if (Network == Network.Main)
			{
				_fallbackBackendUri = new Uri(MainNetFallbackBackendUri);
			}
			else if (Network == Network.TestNet)
			{
				_fallbackBackendUri = new Uri(TestNetFallbackBackendUri);
			}
			else // RegTest
			{
				_fallbackBackendUri = new Uri(RegTestBackendUriV3);
			}

			return _fallbackBackendUri;
		}

		private IPEndPoint _torSocks5EndPoint;
		private int? _mixUntilAnonymitySet;
		private int? _privacyLevelSome;
		private int? _privacyLevelFine;
		private int? _privacyLevelStrong;
		private string _bitcoinCoreDataDir;

		public IPEndPoint GetTorSocks5EndPoint()
		{
			if (_torSocks5EndPoint is null)
			{
				var host = IPAddress.Parse(TorHost);
				_torSocks5EndPoint = new IPEndPoint(host, (int)TorSocks5Port);
			}

			return _torSocks5EndPoint;
		}

		public Config()
		{
			_backendUri = null;
		}

		public Config(string filePath)
		{
			_backendUri = null;
			SetFilePath(filePath);
		}

		public Config(
			Network network,
			string mainNetBackendUriV3,
			string testNetBackendUriV3,
			string mainNetFallbackBackendUri,
			string testNetFallbackBackendUri,
			string regTestBackendUriV3,
			string bitcoinCoreDataDir,
			string torHost,
			int? torSocks5Port,
			int? mixUntilAnonymitySet,
			int? privacyLevelSome,
			int? privacyLevelFine,
			int? privacyLevelStrong)
		{
			Network = Guard.NotNull(nameof(network), network);

			MainNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBackendUriV3), mainNetBackendUriV3);
			TestNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBackendUriV3), testNetBackendUriV3);
			MainNetFallbackBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetFallbackBackendUri), mainNetFallbackBackendUri);
			TestNetFallbackBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetFallbackBackendUri), testNetFallbackBackendUri);
			TestNetBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBackendUriV3), testNetBackendUriV3);
			RegTestBackendUriV3 = Guard.NotNullOrEmptyOrWhitespace(nameof(regTestBackendUriV3), regTestBackendUriV3);

			TorHost = Guard.NotNullOrEmptyOrWhitespace(nameof(torHost), torHost);
			TorSocks5Port = Guard.NotNull(nameof(torSocks5Port), torSocks5Port);

			BitcoinCoreDataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinCoreDataDir), bitcoinCoreDataDir);

			MixUntilAnonymitySet = Guard.NotNull(nameof(mixUntilAnonymitySet), mixUntilAnonymitySet);
			PrivacyLevelSome = Guard.NotNull(nameof(privacyLevelSome), privacyLevelSome);
			PrivacyLevelFine = Guard.NotNull(nameof(privacyLevelFine), privacyLevelFine);
			PrivacyLevelStrong = Guard.NotNull(nameof(privacyLevelStrong), privacyLevelStrong);

			ServiceConfiguration = new ServiceConfiguration(MixUntilAnonymitySet.Value, PrivacyLevelSome.Value, PrivacyLevelFine.Value, PrivacyLevelStrong.Value, BitcoinCoreDataDir);
		}

		/// <inheritdoc />
		public async Task ToFileAsync()
		{
			AssertFilePathSet();

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(FilePath,
			jsonString,
			Encoding.UTF8);
		}

		/// <inheritdoc />
		public async Task LoadOrCreateDefaultFileAsync()
		{
			AssertFilePathSet();

			Network = Network.Main;

			MainNetBackendUriV3 = "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/";
			TestNetBackendUriV3 = "http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion/";
			MainNetFallbackBackendUri = "https://wasabiwallet.io/";
			TestNetFallbackBackendUri = "https://wasabiwallet.co/";
			RegTestBackendUriV3 = "http://localhost:37127/";
			BitcoinCoreDataDir = EnvironmentHelpers.TryGetDefaultBitcoinCoreDataDir() ?? "/bitcoin"; // whatever

			TorHost = IPAddress.Loopback.ToString();
			TorSocks5Port = 9050;

			MixUntilAnonymitySet = 50;
			PrivacyLevelSome = 2;
			PrivacyLevelFine = 21;
			PrivacyLevelStrong = 50;

			ServiceConfiguration = new ServiceConfiguration(MixUntilAnonymitySet.Value, PrivacyLevelSome.Value, PrivacyLevelFine.Value, PrivacyLevelStrong.Value, BitcoinCoreDataDir);

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo<Config>($"{nameof(Config)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				await LoadFileAsync();
			}

			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();

			await ToFileAsync();
		}

		public async Task LoadFileAsync()
		{
			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
			var config = JsonConvert.DeserializeObject<Config>(jsonString);

			Network = config.Network ?? Network;

			MainNetBackendUriV3 = config.MainNetBackendUriV3 ?? MainNetBackendUriV3;
			TestNetBackendUriV3 = config.TestNetBackendUriV3 ?? TestNetBackendUriV3;
			MainNetFallbackBackendUri = config.MainNetFallbackBackendUri ?? MainNetFallbackBackendUri;
			TestNetFallbackBackendUri = config.TestNetFallbackBackendUri ?? TestNetFallbackBackendUri;
			RegTestBackendUriV3 = config.RegTestBackendUriV3 ?? RegTestBackendUriV3;
			BitcoinCoreDataDir = config.BitcoinCoreDataDir ?? BitcoinCoreDataDir;

			TorHost = config.TorHost ?? TorHost;
			TorSocks5Port = config.TorSocks5Port ?? TorSocks5Port;

			MixUntilAnonymitySet = config.MixUntilAnonymitySet ?? MixUntilAnonymitySet;
			PrivacyLevelSome = config.PrivacyLevelSome ?? PrivacyLevelSome;
			PrivacyLevelFine = config.PrivacyLevelFine ?? PrivacyLevelFine;
			PrivacyLevelStrong = config.PrivacyLevelStrong ?? PrivacyLevelStrong;

			ServiceConfiguration = config.ServiceConfiguration ?? ServiceConfiguration;

			// Just debug convenience.
			_backendUri = GetCurrentBackendUri();
		}

		/// <inheritdoc />
		public async Task<bool> CheckFileChangeAsync()
		{
			AssertFilePathSet();

			if (!File.Exists(FilePath))
			{
				throw new FileNotFoundException($"{nameof(Config)} file did not exist at path: `{FilePath}`.");
			}

			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
			var config = JsonConvert.DeserializeObject<Config>(jsonString);

			if (Network != config.Network)
			{
				return true;
			}

			if (!MainNetBackendUriV3.Equals(config.MainNetBackendUriV3, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!TestNetBackendUriV3.Equals(config.TestNetBackendUriV3, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!MainNetFallbackBackendUri.Equals(config.MainNetFallbackBackendUri, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!TestNetFallbackBackendUri.Equals(config.TestNetFallbackBackendUri, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!RegTestBackendUriV3.Equals(config.RegTestBackendUriV3, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (!TorHost.Equals(config.TorHost, StringComparison.Ordinal))
			{
				return true;
			}
			if (TorSocks5Port != config.TorSocks5Port)
			{
				return true;
			}

			if (BitcoinCoreDataDir != config.BitcoinCoreDataDir) // case sensitive
			{
				return true;
			}

			if (MixUntilAnonymitySet != config.MixUntilAnonymitySet)
			{
				return true;
			}
			if (PrivacyLevelSome != config.PrivacyLevelSome)
			{
				return true;
			}
			if (PrivacyLevelFine != config.PrivacyLevelFine)
			{
				return true;
			}
			if (PrivacyLevelStrong != config.PrivacyLevelStrong)
			{
				return true;
			}

			return false;
		}

		/// <inheritdoc />
		public void SetFilePath(string path)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
		}

		/// <inheritdoc />
		public void AssertFilePathSet()
		{
			if (FilePath is null) throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
		}
	}
}
