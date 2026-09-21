FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GreenCityReporter/GreenCityReporter.csproj GreenCityReporter/
RUN dotnet restore GreenCityReporter/GreenCityReporter.csproj

COPY . .
RUN dotnet publish GreenCityReporter/GreenCityReporter.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet GreenCityReporter.dll"]
