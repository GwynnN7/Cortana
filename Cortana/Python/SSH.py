import os, sys
user = sys.argv[1]
ip = sys.argv[2]
command = " ".join(sys.argv[3:])
os.system(f"ssh {user}@{ip} {command}")