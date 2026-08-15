FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/OrderCreation.Api/OrderCreation.Api.csproj src/OrderCreation.Api/
RUN dotnet restore src/OrderCreation.Api/OrderCreation.Api.csproj

COPY src/OrderCreation.Api/ src/OrderCreation.Api/
RUN dotnet publish src/OrderCreation.Api/OrderCreation.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OrderCreation.Api.dll"]
