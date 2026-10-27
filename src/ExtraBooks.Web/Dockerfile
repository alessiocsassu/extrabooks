FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/ExtraBooks.Contracts/ExtraBooks.Contracts.csproj src/ExtraBooks.Contracts/
COPY src/ExtraBooks.Web/ExtraBooks.Web.csproj src/ExtraBooks.Web/
RUN dotnet restore src/ExtraBooks.Web/ExtraBooks.Web.csproj
COPY src/ExtraBooks.Contracts/ src/ExtraBooks.Contracts/
COPY src/ExtraBooks.Web/ src/ExtraBooks.Web/
RUN dotnet publish src/ExtraBooks.Web/ExtraBooks.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ExtraBooks.Web.dll"]
