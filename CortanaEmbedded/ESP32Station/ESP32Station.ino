#include <WiFi.h>
#include <Wire.h>
#include <Adafruit_AHTX0.h>
#include "SparkFun_ENS160.h"
#include <BH1750.h>
#include "Adafruit_SHT4x.h"

#include "secrets.h"

const int led = 19;
const int motion_sensor = 23;

WiFiClient client;

Adafruit_AHTX0 aht;
SparkFun_ENS160 myENS;
BH1750 lightMeter;
Adafruit_SHT4x sht4 = Adafruit_SHT4x();

unsigned long tcpTime;
const int transmissionTime = 5000;

unsigned long factsTime;
const unsigned long factsInterval = 300000;

int currentMotion = LOW;
int lastSentLedState = LOW;

float roomTemp = 0.0;
float roomHumidity = 0.0;
float temp = 0.0;
float humidity = 0.0;
int luxLight = 0;
uint16_t eco2 = 0;
uint16_t tvoc = 0;

bool busResponds(int sda, int scl)
{
  Wire.end();
  if (!Wire.begin(sda, scl)) return false;

  for (uint8_t address = 1; address < 127; address++)
  {
    Wire.beginTransmission(address);
    if (Wire.endTransmission() == 0) return true;
  }

  return false;
}

void startI2C()
{
  if (busResponds(21, 22)) return;
  if (busResponds(13, 16)) return;
  if (busResponds(13, 33)) return;

  Wire.end();
  Wire.begin();
}

void announce()
{
  client.print(F("{\"type\":\"hello\",\"magic\":\"cortana\",\"version\":1,"
                 "\"source\":\"station\",\"kind\":\"Station\","
                 "\"outputs\":[],"
                 "\"inputs\":[\"motion\",\"light\",\"temperature\",\"humidity\",\"co2\",\"tvoc\",\"air_temperature\"],"
                 "\"facts\":{\"name\":\"station\",\"os\":\"ESP32\"}}\n"));

  describe();
}

// What the board says about itself: not a reading, but what it is and how it is doing
void describe()
{
  unsigned long seconds = millis() / 1000UL;

  char buffer[220];
  snprintf(buffer, sizeof(buffer),
           "{\"type\":\"facts\",\"values\":{"
           "\"name\":\"station\",\"os\":\"ESP32\","
           "\"uptime\":\"%luh %lum\",\"ip\":\"%s\",\"signal\":\"%d dBm\","
           "\"memory\":\"%u KB free\"}}\n",
           seconds / 3600UL, (seconds % 3600UL) / 60UL,
           WiFi.localIP().toString().c_str(), WiFi.RSSI(),
           (unsigned)(ESP.getFreeHeap() / 1024));

  client.print(buffer);
}

void setup()
{
  startI2C();

  pinMode(motion_sensor, INPUT);
  pinMode(led, OUTPUT);

  aht.begin();
  myENS.begin(); 
  myENS.setOperatingMode(SFE_ENS160_STANDARD);
  lightMeter.begin();
  sht4.begin();
  sht4.setPrecision(SHT4X_HIGH_PRECISION);
  sht4.setHeater(SHT4X_NO_HEATER);

  connectToWiFi();
  checkTCPConnection();
  announce();

  tcpTime = millis();
}

void loop()
{
  currentMotion = digitalRead(motion_sensor);
  digitalWrite(led, currentMotion);

  unsigned long newTime = millis() - tcpTime;

  if ((newTime >= transmissionTime) || (currentMotion != lastSentLedState))
  {
    checkTCPConnection();

    if (newTime >= transmissionTime) 
    {
      readSensors();
      tcpTime = millis(); 

      if (millis() - factsTime >= factsInterval)
      {
        describe();
        factsTime = millis();
      }
    }

    char buff[320];
    snprintf(buff, 320, "{\"type\":\"reading\",\"values\":{\"motion\":%d,\"light\":%d,\"temperature\":%.2f,\"humidity\":%.2f,\"co2\":%u,\"tvoc\":%u,\"air_temperature\":%.2f}}\n", currentMotion, luxLight, roomTemp, roomHumidity, eco2, tvoc, temp);

    client.print(buff);
    lastSentLedState = currentMotion;
  }

  delay(50); 
}

void readSensors()
{
  sensors_event_t humidityEvent, tempEvent;
  aht.getEvent(&humidityEvent, &tempEvent);
  temp = tempEvent.temperature; // Degrees Celsius
  humidity = humidityEvent.relative_humidity; // Percent Relative Humidity

  myENS.setTempCompensationCelsius(temp);
  myENS.setRHCompensationFloat(humidity);

  if (myENS.checkDataStatus()) 
  {
    eco2 = myENS.getECO2(); // PPM of equivalent CO2 in the air
    tvoc = myENS.getTVOC(); // PPB of Total Volatile Organic Compounds (TVOC) in the air
  }

  luxLight = lightMeter.readLightLevel(); // Lux value

  sensors_event_t shtHumidity, shtTemp;
  sht4.getEvent(&shtHumidity, &shtTemp);

  roomTemp = shtTemp.temperature; // Degrees Celsius
  roomHumidity = shtHumidity.relative_humidity; // Percent Relative Humidity
}

void checkTCPConnection()
{
  if (!client.connected())
  {
    while (!client.connect(CORTANA_IP, CORTANA_PORT))
    {
      delay(1500);
      connectToWiFi();
    }
    announce();
  }
}

void connectToWiFi()
{
  int tryCount = 0;
  while (WiFi.status() != WL_CONNECTED)
  {
    if (tryCount == 10)
    {
      WiFi.disconnect();
      ESP.restart();
    }

    if (client.connected())
    {
      client.stop();
    }
    WiFi.disconnect();
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    WiFi.setSleep(true);

    tryCount++;
    delay(1500);
  }
}