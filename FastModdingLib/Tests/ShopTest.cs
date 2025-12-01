using Duckov.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FastModdingLib.Tests
{
    public static class ShopTest
    {
        public static void Test()
        {
            ShopGoodsData data = new ShopGoodsData
            {
                merchantProfileID = "Merchant_Normal",
                typeID = 1259,
                maxStock = 1,
                forceUnlock = false,
                priceFactor = 1F,
                possibility = 1F
            };

            ShopUtils.AddGoods(data);
        }
    }
}
