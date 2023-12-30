import board, digitalio
import adafruit_ssd1306

oled_reset = digitalio.DigitalInOut(board.D5)
WIDTH = 128
HEIGHT = 32

i2c = board.I2C()
oled = adafruit_ssd1306.SSD1306_I2C(WIDTH, HEIGHT, i2c, addr=0x3C, reset=oled_reset)
oled.poweroff()