import os, sys
mac = sys.argv[1]
os.system(f"sudo etherwake -i eth0 {mac}")
