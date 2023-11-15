import os, sys
user = sys.argv[1]
ip = sys.argv[2]
command = ' '.join(sys.argv[3:])
try:
	x = os.system(f"ssh {user}@{ip} {command}")
	print(x)
except:
	print("65280")
