FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Inverflex/Inverflex.csproj Inverflex/
RUN dotnet restore Inverflex/Inverflex.csproj
COPY Inverflex/ Inverflex/
RUN dotnet publish Inverflex/Inverflex.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Inverflex.dll"]
