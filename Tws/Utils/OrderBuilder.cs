using IBApi;

namespace ArcTriggerUI.Tws.Utils
{
    public class OrderBuilder
    {
        private readonly Order _order;

        public OrderBuilder()
        {
            _order = new Order
            {
                Tif = "DAY",
                Transmit = true
            };
        }

        public OrderBuilder WithAction(string action)
        {
            _order.Action = action;
            return this;
        }

        public OrderBuilder WithOpenClose(string oc)
        {
            _order.OpenClose = oc;
            return this;
        }
           // "C" kapat
        public OrderBuilder WithTriggerMethod(int m)
        {
            _order.TriggerMethod = m;
            return this;
        }
         // 0=Default
        public OrderBuilder WithTrailingPercent(double p)
        {
            _order.TrailingPercent = p;
            return this;
        }

        public OrderBuilder WithTrailStopPrice(double p)
        {
            _order.TrailStopPrice = p;
            return this;
        }

        public OrderBuilder WithAccount(string acct)
        {
            _order.Account = acct;
            return this;
        }

        public OrderBuilder WithOrderType(string type)
        {
            _order.OrderType = type;
            return this;
        }

        public OrderBuilder WithQuantity(int quantity)
        {
            _order.TotalQuantity = quantity;
            return this;
        }

        public OrderBuilder WithLimitPrice(double price)
        {
            _order.LmtPrice = price;
            return this;
        }

        public OrderBuilder WithStopPrice(double price)
        {
            _order.AuxPrice = price;
            return this;
        }

        public OrderBuilder WithTif(string tif)
        {
            _order.Tif = tif;
            return this;
        }

        public OrderBuilder WithTransmit(bool transmit)
        {
            _order.Transmit = transmit;
            return this;
        }

        public OrderBuilder WithOutsideRth(bool allow)
        {
            _order.OutsideRth = allow;
            return this;
        }

        public OrderBuilder WithGoodAfterTime(string time)
        {
            _order.GoodAfterTime = time; // format: yyyyMMdd HH:mm:ss
            return this;
        }

        public OrderBuilder WithGoodTillDate(string time)
        {
            _order.GoodTillDate = time; // format: yyyyMMdd HH:mm:ss
            return this;
        }

        public OrderBuilder WithParentId(int parentId)
        {
            _order.ParentId = parentId;
            return this;
        }

        public OrderBuilder WithOcaGroup(string groupId, int type = 1)
        {
            _order.OcaGroup = groupId;
            _order.OcaType = type; // 1=CancelAll, 2=Reduce
            return this;
        }

        public OrderBuilder WithAuxPrice(double price)
        {
            _order.AuxPrice = price;
            return this;
        }

        public Order Build() => _order;
    }
}
