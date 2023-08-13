FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Wavee.Vorbis.Test/Wavee.Vorbis.Test.csproj", "Wavee.Vorbis.Test/"]
RUN dotnet restore "Wavee.Vorbis.Test/Wavee.Vorbis.Test.csproj"
COPY . .
WORKDIR "/src/Wavee.Vorbis.Test"
RUN dotnet build "Wavee.Vorbis.Test.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wavee.Vorbis.Test.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Wavee.Vorbis.Test.dll"]
