import os, sys
user = sys.argv[1]
ip = sys.argv[2]
command = ' '.join(sys.argv[3:])
try:
	exitCode = os.system(f"ssh -o ConnectTimeout=3 {user}@{ip} {command}")
	print(exitCode)
except:
	print("65280")
