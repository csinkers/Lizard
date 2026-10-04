set dotenv-load

[windows]
set shell := ["C:/Program Files/Git/bin/bash.exe", "-cu"]

build:
    @dotnet build

run:
    @dotnet run --project src/Lizard/Lizard.csproj

regen-cpp: _check
    dotnet run --project src/Lizard.CodeGen/Lizard.CodeGen.fsproj -- \
        src/LizardProtocol1.lproto              \
        -o ${DOSBOX_STAGING_PATH}/src/debugger/ \
        -ns LizardComms \
        -cpp -v

_check:
    @if [ "${DOSBOX_STAGING_PATH:-unset}" = "unset" ];                             \
    then                                                                           \
        echo "DOSBOX_STAGING_PATH not set, add a .env file with file paths, e.g."; \
        echo "Note: All paths must be absolute";         \
        echo ;                                           \
        echo "DOSBOX_STAGING_PATH=~/git/dosbox-staging"; \
        echo ;                                           \
        exit 1;                                          \
    fi

    @if [ ! -d "${DOSBOX_STAGING_PATH}" ]; then                                    \
        echo "DOSBox Staging repo directory \"${DOSBOX_STAGING_PATH}\" not found"; \
        exit 1;                                                                    \
    fi
