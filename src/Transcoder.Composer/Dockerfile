FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN for i in $(seq 1 3); do NUGET_PACKAGES=/root/.nuget/packages dotnet restore transcoder.sln -r linux-x64 -p:PublishReadyToRun=true && break || sleep 5; done
WORKDIR "/src/src/Transcoder.Composer"
RUN dotnet publish Transcoder.Composer.csproj --no-restore -c $BUILD_CONFIGURATION -o /publish -r linux-x64 --no-self-contained -p:UseAppHost=false -p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
USER $APP_UID
WORKDIR /app
COPY --from=build /publish /app
RUN apt-get update \
    && apt-get install -y apt-utils libgdiplus libc6-dev \
    && apt-get install -y ffmpeg
ENTRYPOINT ["dotnet", "Transcoder.Composer.dll"]
