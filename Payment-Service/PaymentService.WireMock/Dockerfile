FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Payment-Service/PaymentService.WireMock/PaymentService.WireMock.csproj Payment-Service/PaymentService.WireMock/
RUN dotnet restore Payment-Service/PaymentService.WireMock/PaymentService.WireMock.csproj

COPY Payment-Service/PaymentService.WireMock/ Payment-Service/PaymentService.WireMock/
WORKDIR /src/Payment-Service/PaymentService.WireMock
RUN dotnet publish --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5127
ENTRYPOINT ["dotnet", "PaymentService.WireMock.dll"]
