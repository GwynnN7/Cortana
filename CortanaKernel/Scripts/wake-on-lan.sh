#!/usr/bin/zsh

if (($# == 1)) then
  sudo wakeonlan "$1";
  sudo etherwake "$1";
fi
