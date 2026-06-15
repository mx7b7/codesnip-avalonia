#!/usr/bin/env bash

set -euo pipefail

PROJECT_NAME="CodeSnip"

OS_TYPE=$(uname -s)
if [ "$OS_TYPE" == "Darwin" ]; then
    CONFIG="Release-Mac-ARM"
else
    CONFIG="Release-Linux"
fi

while true; do
    clear

    echo "======================================"
    echo "        $PROJECT_NAME Build System ($OS_TYPE)"
    echo "======================================"
    echo
    echo "1) Clean"
    echo "2) Build"
    echo "3) Publish self-contained"
    echo "4) Publish self-contained trimmed"
    echo "0) Exit"
    echo

    read -rp "Choice: " choice

    case "$choice" in
        1)
            echo -e "\nCleaning..."
            dotnet clean
            ;;

        2)
            echo -e "\nBuilding $CONFIG..."
            dotnet build -c "$CONFIG"
            ;;

        3)
            echo -e "\nPublishing self-contained ($CONFIG)..."
            dotnet publish \
                -c "$CONFIG" \
                --self-contained true \
                -p:PublishSingleFile=false
            ;;

        4)
            echo -e "\nPublishing self-contained trimmed ($CONFIG)..."
            dotnet publish \
                -c "$CONFIG" \
                --self-contained true \
                -p:PublishSingleFile=false \
                -p:PublishTrimmed=true
            ;;

        0)
            exit 0
            ;;

        *)
            echo -e "\nInvalid option."
            ;;
    esac

    echo
    read -rp "Press Enter to continue..."
done