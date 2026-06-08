# deploy-stamp: 2026-06-02-watchdog-fix-full-rebuild
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy repo and restore within service folder
COPY . .
WORKDIR /src/src/auth-api
RUN dotnet restore "auth-api.csproj"
RUN dotnet publish "auth-api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "auth-api.dll"]
