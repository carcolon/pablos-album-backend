FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BabyAlbum.slnx ./
COPY src/BabyAlbum.Domain/BabyAlbum.Domain.csproj src/BabyAlbum.Domain/
COPY src/BabyAlbum.Contracts/BabyAlbum.Contracts.csproj src/BabyAlbum.Contracts/
COPY src/BabyAlbum.Application/BabyAlbum.Application.csproj src/BabyAlbum.Application/
COPY src/BabyAlbum.Infrastructure/BabyAlbum.Infrastructure.csproj src/BabyAlbum.Infrastructure/
COPY src/BabyAlbum.API/BabyAlbum.API.csproj src/BabyAlbum.API/
RUN dotnet restore BabyAlbum.slnx

COPY . .
RUN dotnet publish src/BabyAlbum.API/BabyAlbum.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BabyAlbum.API.dll"]
