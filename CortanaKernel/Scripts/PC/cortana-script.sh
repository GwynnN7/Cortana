#!/usr/bin/zsh

if [ "$1" = "shutdown" ]; then
  poweroff
elif [ "$1" = "reboot" ]; then
  reboot
elif [ "$1" = "notify" ]; then
    all=( "${@}" )
    substring="${all[*]:1}"
    notify-send -u low -a Cortana -i "./cortana.jpg" "$substring"
fi
echo 0