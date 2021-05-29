using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages.CircuitStatus;
using WalletWasabi.Tor.Control.Utils;

namespace WalletWasabi.Tor.Control.Messages
{
	public class GetInfoCircuitStatusReply
	{
		public GetInfoCircuitStatusReply(IList<CircuitInfo> circuits)
		{
			Circuits = circuits;
		}

		public IList<CircuitInfo> Circuits { get; }

		/// <exception cref="TorControlReplyParseException"/>
		public static GetInfoCircuitStatusReply FromReply(TorControlReply reply)
		{
			if (!reply.Success)
			{
				throw new TorControlReplyParseException("GETINFO[circuit-status]: Expected reply with OK status.");
			}

			if (reply.ResponseLines.First() != "circuit-status=")
			{
				throw new TorControlReplyParseException("GETINFO[circuit-status]: First line is invalid.");
			}

			if (reply.ResponseLines.Last() != ".")
			{
				throw new TorControlReplyParseException("GETINFO[circuit-status]: Last line must be equal to dot ('.').");
			}

			IList<CircuitInfo> circuits = new List<CircuitInfo>();

			foreach (string line in reply.ResponseLines.Skip(1).SkipLast(1))
			{
				(string circuitId, string remainder1) = Tokenizer.ReadUntilSeparator(line);
				(string circuitStatus, string remainder2) = Tokenizer.ReadUntilSeparator(remainder1);

				CircStatus circStatus = Tokenizer.ParseEnumValue(circuitStatus, CircStatus.UNKNOWN);

				string remainder = remainder2;

				List<BuildFlag> buildFlags = new();
				Purpose? purpose = null;
				HsState? hsState = null;
				string? rendQuery = null;
				string? timeCreated = null;
				Reason? reason = null;
				Reason? remoteReason = null;
				string? userName = null;
				string? userPassword = null;
				List<CircPath> circPaths = new();

				// Optional arguments.
				while (remainder != "")
				{
					// Read <PATH>.
					if (remainder.StartsWith("$", StringComparison.Ordinal))
					{
						string pathVal;
						(pathVal, remainder) = Tokenizer.ReadUntilSeparator(remainder);
						circPaths = ParseCircPath(pathVal);

						continue;
					}

					if (remainder.StartsWith("SOCKS_USERNAME", StringComparison.Ordinal))
					{
						(userName, remainder) = Tokenizer.ReadKeyQuotedValueAssignment("SOCKS_USERNAME", remainder);
						continue;
					}
					else if (remainder.StartsWith("SOCKS_PASSWORD", StringComparison.Ordinal))
					{
						(userPassword, remainder) = Tokenizer.ReadKeyQuotedValueAssignment("SOCKS_PASSWORD", remainder);
						continue;
					}

					// Read KEY=VALUE assignments.
					(string key, string value, string rest) = Tokenizer.ReadKeyValueAssignment(remainder);

					if (key == "BUILD_FLAGS")
					{
						string[] flags = value.Split(',');
						buildFlags = flags.Select(x => Tokenizer.ParseEnumValue(x, BuildFlag.UNKNOWN)).ToList();
					}
					else if (key == "PURPOSE")
					{
						purpose = Tokenizer.ParseEnumValue(value, Purpose.UNKNOWN);
					}
					else if (key == "HS_STATE")
					{
						hsState = Tokenizer.ParseEnumValue(value, HsState.UNKNOWN);
					}
					else if (key == "REND_QUERY")
					{
						rendQuery = value;
					}
					else if (key == "TIME_CREATED")
					{
						timeCreated = value;
					}
					else if (key == "REASON")
					{
						reason = Tokenizer.ParseEnumValue(value, Reason.UNKNOWN);
					}
					else if (key == "REMOTE_REASON")
					{
						reason = Tokenizer.ParseEnumValue(value, Reason.UNKNOWN);
					}
					else
					{
						Logger.LogError($"Unknown key '{key}'.");
					}

					remainder = rest;
				}

				CircuitInfo circuitInfo = new(circuitId, circStatus)
				{
					CircPaths = circPaths,
					BuildFlags = buildFlags,
					Purpose = purpose,
					HsState = hsState,
					RendQuery = rendQuery,
					TimeCreated = timeCreated,
					Reason = reason,
					RemoteReason = remoteReason,
					UserName = userName,
					UserPassword = userPassword,
				};

				circuits.Add(circuitInfo);
			}

			return new GetInfoCircuitStatusReply(circuits);
		}

		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">2.4. General-use tokens (see LongName)</seealso>
		private static List<CircPath> ParseCircPath(string paths)
		{
			List<CircPath> result = new();

			foreach (string path in paths.Split(','))
			{
				if (path.IndexOf('=') > -1)
				{
					string[] parts = path.Split('=', count: 2);
					result.Add(new CircPath(FingerPrint: parts[0], Nickname: parts[1]));
				}
				else if (path.IndexOf('~') > -1)
				{
					string[] parts = path.Split('~', count: 2);
					result.Add(new CircPath(FingerPrint: parts[0], Nickname: parts[1]));
				}
				else
				{
					result.Add(new CircPath(FingerPrint: path, Nickname: null));
				}
			}

			return result;
		}
	}
}
