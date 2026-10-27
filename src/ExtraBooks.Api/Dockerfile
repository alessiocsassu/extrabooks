FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY global.json .
COPY src/ExtraBooks.Contracts/ExtraBooks.Contracts.csproj src/ExtraBooks.Contracts/
COPY src/ExtraBooks.Api/ExtraBooks.Api.csproj src/ExtraBooks.Api/
RUN dotnet restore src/ExtraBooks.Api/ExtraBooks.Api.csproj
COPY src/ExtraBooks.Contracts/ src/ExtraBooks.Contracts/
COPY src/ExtraBooks.Api/ src/ExtraBooks.Api/
RUN dotnet publish src/ExtraBooks.Api/ExtraBooks.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ExtraBooks.Api.dll"]
