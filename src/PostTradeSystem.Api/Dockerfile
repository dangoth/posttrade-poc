FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PostTradeSystem.Api/PostTradeSystem.Api.csproj", "src/PostTradeSystem.Api/"]
COPY ["src/PostTradeSystem.Core/PostTradeSystem.Core.csproj", "src/PostTradeSystem.Core/"]
COPY ["src/PostTradeSystem.Infrastructure/PostTradeSystem.Infrastructure.csproj", "src/PostTradeSystem.Infrastructure/"]
RUN dotnet restore "src/PostTradeSystem.Api/PostTradeSystem.Api.csproj"
COPY . .
WORKDIR "/src/src/PostTradeSystem.Api"
RUN dotnet build "PostTradeSystem.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PostTradeSystem.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PostTradeSystem.Api.dll"]