#!/usr/bin/zsh

export CORTANA_PATH=/home/gwynn7/Programming/Cortana

nohup dotnet run --project $CORTANA_PATH/CortanaClient/CortanaClient.csproj > /dev/null 2> /dev/null & disown;
