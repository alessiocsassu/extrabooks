FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /src
COPY global.json .
COPY src/ExtraBooks.Contracts/ src/ExtraBooks.Contracts/
COPY src/ExtraBooks.Api/ src/ExtraBooks.Api/
COPY src/ExtraBooks.Web/ src/ExtraBooks.Web/
COPY tests/ExtraBooks.Tests/ tests/ExtraBooks.Tests/
COPY ExtraBooks.sln .
RUN dotnet restore tests/ExtraBooks.Tests/ExtraBooks.Tests.csproj
RUN dotnet build tests/ExtraBooks.Tests/ExtraBooks.Tests.csproj -c Release --no-restore
ENTRYPOINT ["dotnet", "test", "tests/ExtraBooks.Tests/ExtraBooks.Tests.csproj", "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]
