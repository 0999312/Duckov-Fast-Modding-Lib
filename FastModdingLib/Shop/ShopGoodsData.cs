
using FastModdingLib.Utils;

namespace FastModdingLib
{
    public class ShopGoodsData
    {
        public string merchantProfileID = "Merchant_Normal";
        public int typeID;

        /// <summary>
        /// 可选：物品 Identifier。设置后优先解析为 typeID，
        /// 解析失败时回退到 <see cref="typeID"/>。
        /// </summary>
        public Identifier? itemIdentifier;

        public int maxStock;

        public bool forceUnlock = false;

        public float priceFactor = 1F;

        public float possibility = 1F;

    }
}
