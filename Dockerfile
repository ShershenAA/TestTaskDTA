FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Practitioners.Api/Practitioners.Api.csproj src/Practitioners.Api/
RUN dotnet restore src/Practitioners.Api/Practitioners.Api.csproj
COPY src/ src/
RUN dotnet publish src/Practitioners.Api/Practitioners.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Practitioners.Api.dll"]
