import requests
import json

def is_video_unavailable(url):
    r = requests.get(url)
    return ("video non è più disponibile" in r.text or "video unavailable" in r.text)

with open("Memes.json") as f:
    data = json.load(f)

fixed_data = {}  
for meme in data:
    if(is_video_unavailable(data[meme]["link"])):
        print("Video unavailable: " + meme)
        continue
    fixed_data[meme] = data[meme]

with open("Memes.json", 'w') as f:
    json.dump(fixed_data, f, indent=4)
