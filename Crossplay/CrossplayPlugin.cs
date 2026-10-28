using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Hooks;

namespace Crossplay
{
    [ApiVersion(2, 1)]
    public class CrossplayPlugin : TerrariaPlugin
    {
        /// <summary>Oldest client release that is translated (Terraria 1.4.5.0).</summary>
        private const int OldestSupportedRelease = 313;

        /// <summary>
        /// Releases this fork has actually been tested against. Anything at or
        /// above <see cref="OldestSupportedRelease"/> and below the running server
        /// release is translated too; this table only supplies nice names and
        /// per-version item limits.
        /// </summary>
        private readonly Dictionary<int, string> _knownVersions = new()
        {
            { 313, "v1.4.5.0" },
            { 314, "v1.4.5.1" },
            { 315, "v1.4.5.2" },
            { 316, "v1.4.5.3" },
            { 317, "v1.4.5.4" },
            { 318, "v1.4.5.5" },
            { 319, "v1.4.5.6" },
            { 325, "v1.4.5.7" },
        };

        public override string Name => "Crossplay";

        public override string Author => "Moneylover3246 (fork by Nayetdet, 1.4.5.8 update)";

        public override string Description => "Enables crossplay for Terraria";

        public override Version Version => new("2.5.0");

        public CrossplayConfig Config { get; } = new();

        public int[] ClientVersions { get; } = new int[Main.maxPlayers];

        public static CrossplayPlugin Instance { get; private set; }

        public static string SavePath => Path.Combine(TShock.SavePath, "Crossplay.json");

        /// <summary>
        /// Highest item id each client release understands. Filled in for the
        /// running server release at startup; see <see cref="MaxItemIdFor"/>.
        /// </summary>
        public readonly Dictionary<int, int> MaxItems = new()
        {
            { 313, 6145 },
            { 314, 6145 },
            { 315, 6145 },
            { 316, 6145 },
            { 317, 6145 },
            { 318, 6145 },
            { 319, 6145 },
        };

        private int _hostRelease;
        private int _fallbackMaxItemId = 6145;
        private bool _enabled;

        public CrossplayPlugin(Main game) : base(game)
        {
            Instance = this;
            Order = -1;
        }

        public override void Initialize()
        {
            ServerApi.Hooks.NetSendNetData.Register(this, NetModuleHandler.OnSendNetData);

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData, int.MaxValue);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);

            GeneralHooks.ReloadEvent += OnReload;
        }

        private void OnInitialize(EventArgs args)
        {
            Directory.CreateDirectory(TShock.SavePath);

            bool writeConfig = true;
            if (File.Exists(SavePath))
            {
                Config.Read(SavePath, out writeConfig);
            }
            if (writeConfig)
            {
                Config.Write(SavePath);
            }

            _hostRelease = Main.curRelease;
            _fallbackMaxItemId = MaxItems.Count > 0 ? MaxItems.Values.Max() : 6145;

            if (_hostRelease < OldestSupportedRelease)
            {
                Log($"This server is {Main.versionNumber} (release {_hostRelease}); Crossplay needs 1.4.5.0 or newer. Translation stays off.");
                return;
            }

            _enabled = true;
            RegisterHostVersion();
        }

        /// <summary>
        /// Teaches the plugin about the build it is running on, so a newer hotfix
        /// does not abort startup. Only <see cref="CrossplaySettings.StrictVersionCheck"/>
        /// turns an unknown build back into a hard failure.
        /// </summary>
        private void RegisterHostVersion()
        {
            if (_knownVersions.TryGetValue(_hostRelease, out string knownVersion))
            {
                if (knownVersion != Main.versionNumber)
                {
                    Log($"Release {_hostRelease} is named {knownVersion} in this fork but the server reports {Main.versionNumber}.",
                        debug: true);
                }
                return;
            }

            _knownVersions[_hostRelease] = Main.versionNumber;
            MaxItems[_hostRelease] = _fallbackMaxItemId;

            string message =
                $"Server is {Main.versionNumber} (release {_hostRelease}), newer than the newest build this fork was tested against (1.4.5.6). " +
                $"Translating clients up to {Main.versionNumber}.";

            if (Config.Settings.StrictVersionCheck)
            {
                throw new NotSupportedException(
                    message + " Set strict_version_check to false in Crossplay.json to run anyway.");
            }

            Log(message);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetSendNetData.Deregister(this, NetModuleHandler.OnSendNetData);

                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);

                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnReload(ReloadEventArgs args)
        {
            bool writeConfig = true;
            if (File.Exists(SavePath))
            {
                Config.Read(SavePath, out writeConfig);
            }
            if (writeConfig)
            {
                Config.Write(SavePath);
            }
        }

        private void OnPostInitialize(EventArgs e)
        {
            if (!_enabled)
            {
                return;
            }

            StringBuilder sb = new StringBuilder()
                .Append("Crossplay has been enabled & has whitelisted the following versions:\n")
                .Append(string.Join(", ", _knownVersions.Values.OrderBy(version => version)))
                .Append($"\nServer: {Main.versionNumber} (release {_hostRelease}), {ItemID.Count} item types")
                .Append("\n\nIf there are any issues please report them here: https://github.com/Nayetdet/TerrariaCrossplay");

            Log(sb.ToString());
        }

        private void OnGetData(GetDataEventArgs args)
        {
            if (!_enabled)
            {
                return;
            }

            int index = args.Msg.whoAmI;

            if (ClientVersions[index] == 0 && args.MsgID != PacketTypes.ConnectRequest)
            {
                return;
            }

            switch (args.MsgID)
            {
                case PacketTypes.ConnectRequest:
                    HandleConnectRequest(args, index);
                    break;
                case PacketTypes.PlayerInfo:
                    HandlePlayerInfo(args, index);
                    break;
            }
        }

        private void HandleConnectRequest(GetDataEventArgs args, int index)
        {
            string clientVersion;
            using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
            {
                clientVersion = reader.ReadString();
            }

            Log($"Connect request from index {index}: \"{clientVersion}\"", debug: true);

            string digits = new string(clientVersion
                .Reverse()
                .TakeWhile(char.IsDigit)
                .Reverse()
                .ToArray());

            if (!int.TryParse(digits, out int versionNumber))
            {
                return;
            }

            Log($"Client {index} reports '{clientVersion}' (release {versionNumber})", debug: true);

            if (versionNumber == Main.curRelease)
            {
                ClientVersions[index] = -1;
                return;
            }

            if (!IsTranslatableClient(versionNumber))
            {
                return;
            }

            byte[] connectRequest = new PacketFactory()
                .SetType((short)PacketTypes.ConnectRequest)
                .PackString($"Terraria{Main.curRelease}")
                .GetByteData();

            // The rewritten packet replaces the length, type and payload in place.
            // If the client's version string was longer than the server's there is
            // no room, and guessing would corrupt the rest of the stream.
            int target = args.Index - 3;
            if (target < 0 || target + connectRequest.Length > args.Msg.readBuffer.Length)
            {
                Log($"Cannot rewrite connect packet for index {index}: {connectRequest.Length} bytes do not fit at offset {target}.");
                return;
            }

            ClientVersions[index] = versionNumber;

            Buffer.BlockCopy(connectRequest, 0, args.Msg.readBuffer, target, connectRequest.Length);
            args.Length = connectRequest.Length - 3;

            NetMessage.SendData(9, index, -1, NetworkText.FromLiteral("Fixing Version..."), 1);

            Log($"Changing version of index {index} from {DescribeVersion(versionNumber)} => {Main.versionNumber}");
        }

        private void HandlePlayerInfo(GetDataEventArgs args, int index)
        {
            if (!Config.Settings.SupportJourneyClients)
            {
                return;
            }

            int flagsOffset = FindPlayerInfoFlagsOffset(args.Msg.readBuffer, args.Index, args.Length);
            if (flagsOffset < 0)
            {
                Log($"Could not locate the difficulty byte in PlayerInfo from index {index}.", debug: true);
                return;
            }

            ref byte gameModeFlags = ref args.Msg.readBuffer[flagsOffset];

            if (Main.GameMode == GameModeID.Creative)
            {
                if ((gameModeFlags & 8) == 8)
                {
                    return;
                }

                Log($"Enabled journey mode for index {index}");
                gameModeFlags |= 8;

                if (Main.ServerSideCharacter)
                {
                    NetMessage.SendData(4, index, -1, null, index);
                }

                return;
            }

            if (TShock.Config.Settings.SoftcoreOnly && (gameModeFlags & 3) != 0)
            {
                return;
            }

            if ((gameModeFlags & 8) == 8)
            {
                Log($"Disabled journey mode for index {index}");
                gameModeFlags &= 247;
            }
        }

        /// <summary>
        /// Walks the PlayerInfo layout up to the difficulty BitsByte: player id,
        /// skin, voice variant, voice pitch, hair, name, hair dye, hide visual,
        /// hide misc, then seven colours. TShock parses the same order.
        /// </summary>
        private static int FindPlayerInfoFlagsOffset(byte[] buffer, int start, int length)
        {
            int end = start + length;
            int offset = start;

            offset += 1; // player id
            offset += 1; // skin variant
            offset += 1; // voice variant
            offset += 4; // voice pitch
            offset += 1; // hair

            if (offset >= end)
            {
                return -1;
            }

            int nameLength = 0;
            int shift = 0;

            while (true)
            {
                if (offset >= end)
                {
                    return -1;
                }

                byte current = buffer[offset++];
                nameLength |= (current & 0x7F) << shift;

                if ((current & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
                if (shift > 21)
                {
                    return -1;
                }
            }

            offset += nameLength;

            offset += 1;  // hair dye
            offset += 2;  // hide visual flags
            offset += 1;  // hide misc
            offset += 21; // hair, skin, eye, shirt, under shirt, pants, shoe colours

            return offset < end ? offset : -1;
        }

        private bool IsTranslatableClient(int release)
        {
            return release >= OldestSupportedRelease && release < _hostRelease;
        }

        private string DescribeVersion(int release)
        {
            return _knownVersions.TryGetValue(release, out string version) ? version : $"release {release}";
        }

        public int MaxItemIdFor(int release)
        {
            if (MaxItems.TryGetValue(release, out int maxItemId))
            {
                return maxItemId;
            }

            // Unknown client build. Everything below 313 is refused, and the
            // builds above the newest one with item data (1.4.5.6) are newer,
            // so assume they understand every item this server can send.
            return ItemID.Count;
        }

        private void OnLeave(LeaveEventArgs args)
        {
            ClientVersions[args.Who] = 0;
        }

        public void Log(string message, bool debug = false)
        {
            if (debug)
            {
                if (Config.Settings.DebugMode)
                {
                    TShock.Log.ConsoleInfo($"[Crossplay Debug] {message}");
                }
                return;
            }

            TShock.Log.ConsoleInfo($"[Crossplay] {message}");
        }
    }
}
