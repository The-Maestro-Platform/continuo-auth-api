namespace AuthApi.Contracts.Responses;

public record AppScreensResponse(string AppCode, IEnumerable<object> Screens);
