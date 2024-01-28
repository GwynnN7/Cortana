import os, sys
mode = sys.argv[1]
if(mode == "Reboot"):
	os.system("sudo reboot")
else:
	os.system("sudo shutdown now")