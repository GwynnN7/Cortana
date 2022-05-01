import board, digitalio, sys
from PIL import Image, ImageFont, ImageDraw
import adafruit_ssd1306

oled_reset = digitalio.DigitalInOut(board.D5)
WIDTH = 128
HEIGHT = 32

i2c = board.I2C()
oled = adafruit_ssd1306.SSD1306_I2C(WIDTH, HEIGHT, i2c, addr=0x3C, reset=oled_reset)
oled.poweron()

image = Image.open("Images/3.png")
image = image.resize((128, 32))
frame_image = Image.new("1", (128, 32))
thresh = 50
fn = lambda x : 255 if x > thresh else 0

font = ImageFont.load_default()
font.size = 400
(font_width, font_height) = font.getsize("Cortana")
draw = ImageDraw.Draw(image)
draw.text(
    (oled.width // 2 - font_width // 2, oled.height // 2 - font_height // 2),
    "Cortana",
    font=font,
    fill=255,
)
r = image.convert('L').point(fn, mode='1')
frame_image.paste(r,(WIDTH//2 - image.width//2, HEIGHT//2 - image.height//2))
oled.image(frame_image)

oled.show()
