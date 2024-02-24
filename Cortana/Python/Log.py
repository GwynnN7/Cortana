import os, sys
filename = sys.argv[1]
log = " ".join(sys.argv[2:])+"\n"
os.system(f"echo {log} >> $HOME/{filename}.txt")
