# The app, and no content: not a campaign, and not the sample pack either. At run time the app looks
# for its content in /app/Maps and /app/data, found by walking up from /app, or in the folder
# Content__Root names. Mount a campaign there, or build an image FROM this one that copies it in.
#
#   docker build -t dungeontable-app .
#   docker run -p 8080:8080 -e Auth__Passphrase=<choose one> -e Content__Root=/content \
#     -v "$PWD/samples/demo:/content" dungeontable-app
#
# Pinned, not floating: this is the SDK in global.json, the one the lock files were written with, so
# the image builds the same way every time. Keep this tag, global.json, the assets pin in
# DungeonTable.Web.csproj and Mvc.Testing in step.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY . .
RUN dotnet restore DungeonTable.Web/DungeonTable.Web.csproj --locked-mode
RUN dotnet publish DungeonTable.Web/DungeonTable.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY LICENSE .
ENTRYPOINT ["dotnet", "DungeonTable.Web.dll"]
