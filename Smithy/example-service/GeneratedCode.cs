namespace mws.market;

public class GetStockPricesInput
{
    public string symbol { get; }
}

public class GetStockPricesOutput
{
    public double ask { get; }
    public double bid { get; }
    public double last { get; }
}

public class PlaceOrderInput
{
    public string symbol { get; }
    public string type { get; }
    public int quantity { get; }
    public double limit { get; }
}

public class PlaceOrderOutput
{
    public string orderId { get; }
}

public interface IMarketService
{
    Task<GetStockPricesOutput> GetStockPrices(GetStockPricesInput input);
    Task<PlaceOrderOutput> PlaceOrder(PlaceOrderInput input);
}

public class MarketServiceMock : IMarketService
{
    public async Task<GetStockPricesOutput> GetStockPrices(GetStockPricesInput input)
    {
        return new GetStockPricesOutput{};
    }

    public Task<PlaceOrderOutput> PlaceOrder(PlaceOrderInput input)
    {
        throw new NotImplementedException();
    }
}
