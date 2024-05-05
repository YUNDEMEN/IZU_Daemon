FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /source/izu

# copy csproj and restore as distinct layers
COPY *.csproj /source/izu
RUN dotnet restore

# copy and publish app and libraries
COPY . /source/izu
WORKDIR /source/izu
RUN dotnet publish -c release -o /izu --no-restore


# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 8080
WORKDIR /izu
COPY --from=build /izu .
USER $APP_UID
ENTRYPOINT ["dotnet", "IZUE.dll"]