FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish Service/CentralService/CentralService.csproj -c Release -o /app/publish /p:UseAppHost=false /p:RestoreSources=https://api.nuget.org/v3/index.json /p:RestoreIgnoreFailedSources=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:15700
EXPOSE 15700

ENTRYPOINT ["dotnet", "CentralService.dll"]
