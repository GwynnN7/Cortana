#!/usr/bin/zsh

if (($# == 1)) then
  if [ "$1" = "shutdown" ]; then
  	  sudo shutdown now;
  elif [ "$1" = "reboot" ]; then
      sudo reboot;
  fi
fi
