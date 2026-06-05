#!/usr/bin/env bash
# Purpose: Bash entry point for ModularPipelines build system

bash_source="${BASH_SOURCE[0]}"
while [ -h "$bash_source" ]; do # resolve $bash_source until the file is no longer a symlink
  scriptroot="$( cd -P "$( dirname "$bash_source" )" && pwd )"
  bash_source="$(readlink "$bash_source")"
  [[ $bash_source != /* ]] && bash_source="$scriptroot/$bash_source" # if $bash_source was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
scriptroot="$( cd -P "$( dirname "$bash_source" )" && pwd )"

###########################################################################
# CONFIGURATION
###########################################################################

BUILD_PROJECT_FILE="$scriptroot/build/Build.csproj"
TEMP_DIRECTORY="$scriptroot/.nuke/temp"

DOTNET_GLOBAL_FILE="$scriptroot/global.json"
DOTNET_INSTALL_URL="https://dot.net/v1/dotnet-install.sh"
DOTNET_CHANNEL="STS"

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_MULTILEVEL_LOOKUP=0

###########################################################################
# EXECUTION
###########################################################################

function FirstJsonValue {
    perl -nle 'print $1 if m{"'"$1"'": "([^"]+)",?}' <<< "${@:2}"
}

# If dotnet CLI is installed globally and it matches requested version, use for execution
if [ -x "$(command -v dotnet)" ] && dotnet --version &>/dev/null; then
    export DOTNET_EXE="$(command -v dotnet)"
else
    # Download install script
    DOTNET_INSTALL_FILE="$TEMP_DIRECTORY/dotnet-install.sh"
    mkdir -p "$TEMP_DIRECTORY"
    curl -Lsfo "$DOTNET_INSTALL_FILE" "$DOTNET_INSTALL_URL"
    chmod +x "$DOTNET_INSTALL_FILE"

    # If global.json exists, load expected version
    if [[ -f "$DOTNET_GLOBAL_FILE" ]]; then
        DOTNET_VERSION=$(FirstJsonValue "version" "$(cat "$DOTNET_GLOBAL_FILE")")
        if [[ "$DOTNET_VERSION" == ""  ]]; then
            unset DOTNET_VERSION
        fi
    fi

    # Install by channel or version
    DOTNET_DIRECTORY="$TEMP_DIRECTORY/dotnet-unix"
    if [[ -z ${DOTNET_VERSION+x} ]]; then
        "$DOTNET_INSTALL_FILE" --install-dir "$DOTNET_DIRECTORY" --channel "$DOTNET_CHANNEL" --no-path
    else
        "$DOTNET_INSTALL_FILE" --install-dir "$DOTNET_DIRECTORY" --version "$DOTNET_VERSION" --no-path
    fi
    export DOTNET_EXE="$DOTNET_DIRECTORY/dotnet"
fi

echo "Microsoft (R) .NET SDK version $("$DOTNET_EXE" --version)"

"$DOTNET_EXE" run --project "$BUILD_PROJECT_FILE" -- "$@"
