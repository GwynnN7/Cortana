#include <WiFi.h>
#include <Wire.h>
#include <Adafruit_AHTX0.h>
#include "SparkFun_ENS160.h"
#include <BH1750.h>
#include "Adafruit_SHT4x.h"

const char WIFI_SSID[] = "HomeLifeWiFi";
const char WIFI_PASSWORD[] = "HomeLifeCheru9";
const char CORTANA_IP[] = "192.168.1.117";
const int CORTANA_PORT = 5116;

const int led = 19;
const int motion_sensor = 23;

WiFiClient client;

Adafruit_AHTX0 aht;
SparkFun_ENS160 myENS;
BH1750 lightMeter;
Adafruit_SHT4x sht4 = Adafruit_SHT4x();

unsigned long tcpTime;
const int transmissionTime = 5000;

int currentMotion = LOW;
int lastSentLedState = LOW;

float roomTemp = 0.0;
float roomHumidity = 0.0;
float temp = 0.0;
float humidity = 0.0;
int luxLight = 0;
uint16_t eco2 = 0;
uint16_t tvoc = 0;

void setup()
{
  Wire.begin(); 

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
  client.print("esp32");

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
    }

    char buff[200]; 
    snprintf(buff, 200, "{ \"motion\": %d, \"light\": %d, \"temperature\": %.2f, \"humidity\": %.2f, \"eco2\": %u, \"tvoc\": %u }", currentMotion, luxLight, roomTemp, roomHumidity, eco2, tvoc);
    
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
    client.print("esp32");
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
    WiFi.setSleep(false);

    tryCount++;
    delay(1500);
  }
}