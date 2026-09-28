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
    }
}
