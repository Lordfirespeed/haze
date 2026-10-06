using System;
using System.Collections.Generic;
using SteamKit2;

namespace Haze.Steam;

public static class ProductInfoExtensions
{
    extension(SteamApps.PICSProductInfoCallback.PICSProductInfo productInfo)
    {
        public IEnumerable<uint> GetPackageAppIds()
        {
            return productInfo.KeyValues["appids"].AsUnsignedIntegerArray();
        }

        public IEnumerable<uint> GetPackageDepotIds()
        {
            return productInfo.KeyValues["depotids"].AsUnsignedIntegerArray();
        }

        public IEnumerable<DepotKeyValue> GetAppDepots()
        {
            var depots = productInfo.KeyValues["depots"];
            if (ReferenceEquals(depots, KeyValue.Invalid)) yield break;
            foreach (var maybeDepot in depots.Children) {
                var isDepot = uint.TryParse(maybeDepot.Name, out var depotId);
                if (!isDepot) continue;
                yield return new DepotKeyValue(maybeDepot);
            }
        }
    }

    public record struct DepotKeyValue(KeyValue KeyValue)
    {
        public KeyValue this[string key] => KeyValue[key];

        public uint DepotId => uint.Parse(KeyValue.Name!);

        private KeyValue OSListKeyValue => this["config"]["oslist"];
        public string[]? OSList => OSListKeyValue.IsValid ? OSListKeyValue.AsString()!.Split(",") : null;
        private KeyValue OSArchKeyValue => this["config"]["osarch"];
        public string? OSArch => OSArchKeyValue.IsValid ? OSArchKeyValue.AsString() : null;
        private KeyValue LanguageKeyValue => this["config"]["language"];
        public string? Language => String.IsNullOrWhiteSpace(LanguageKeyValue.Value) ? null : LanguageKeyValue.Value;
        /**
         * This seems to be present on a subset of DLC depots (depots with <see cref="DLCAppId"/> set).
         * When it is present, it is always set to the same app ID as <see cref="DLCAppId"/>.
         */
        public KeyValue OptionalDLCKeyValue => this["config"]["optionaldlc"];
        public KeyValue LowViolenceKeyValue => this["config"]["lowviolence"];
        public KeyValue HighQualityAudioKeyValue => this["config"]["highqualityaudio"];
        public KeyValue SteamDeckKeyValue => this["config"]["steamdeck"];
        public KeyValue RealmKeyValue => this["config"]["realm"];
        public KeyValue ModKeyValue => this["config"]["mod"];

        public uint DepotFromAppId => this["depotfromapp"].AsUnsignedInteger();
        public uint DLCAppId => this["dlcappid"].AsUnsignedInteger();
        public bool Optional => this["optional"].AsBoolean();
        public bool SharedInstall => this["sharedinstall"].AsBoolean();
        public uint SharedDepotType => this["shareddepottype"].AsUnsignedInteger();
        public bool SystemDefined => this["systemdefined"].AsBoolean();

        public KeyValue ManifestsKeyValue => this["manifests"];
        public bool IsUnused => ManifestsKeyValue.IsInvalid;
    }
}
