# syntax=docker/dockerfile:1

################################################################################
# Build stage
################################################################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

COPY . /source
WORKDIR /source/src/Mofam.CMS

ARG TARGETARCH

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish -a ${TARGETARCH/amd64/x64} --use-current-runtime --self-contained false -o /app

################################################################################
# Runtime stage
################################################################################
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app .

# Create the Umbraco directories and give the non-root user write access
RUN mkdir -p /app/wwwroot/media \
    && mkdir -p /app/wwwroot/scripts \
    && mkdir -p /app/wwwroot/css \
    && mkdir -p /app/umbraco/Data \
    && mkdir -p /app/umbraco/Logs \
    && chown -R $APP_UID:$APP_UID /app

USER $APP_UID

ENTRYPOINT ["dotnet", "Mofam.CMS.dll"]
