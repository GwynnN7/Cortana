import os, sys
mac = sys.argv[1]
os.system(f"sudo wakeonlan {mac}") #LAN
os.system(f"sudo etherwake {mac}") #WLAN