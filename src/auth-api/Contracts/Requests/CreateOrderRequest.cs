namespace AuthApi.Contracts.Requests;

public record CreateOrderRequest(string ProductId, int Quantity, string? Notes);
