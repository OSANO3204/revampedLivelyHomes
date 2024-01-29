
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["HousingProject.API.csproj", "."]
RUN dotnet restore "./HousingProject.API.csproj"
COPY . .
WORKDIR "/src/."

RUN dotnet build "HousingProject.API.csproj" -c Release -o /app/build


FROM build AS publish
RUN dotnet publish "HousingProject.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HousingProject.API.dll"]
