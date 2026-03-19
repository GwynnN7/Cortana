#include <WiFi.h>
#include <OneWire.h>
#include <DallasTemperature.h>

const char WIFI_SSID[] = "HomeLifeWiFi";
const char WIFI_PASSWORD[] = "HomeLifeCheru9";
const char CORTANA_IP[] = "192.168.1.117";
const int CORTANA_PORT = 5116;

const int led = 21;
const int light_sensor = 35;
const int motion_sensor = 23;
const int temp_sensor = 22;

OneWire oneWire(temp_sensor);
DallasTemperature DS18B20(&oneWire);
WiFiClient client;

unsigned long tcpTime;

const int transmissionTime = 5000;

int currentMotion = LOW;
int lastSentLedState = LOW;

float temp;
int light;

void setup()
{
  DS18B20.begin();
  DS18B20.setWaitForConversion(false);

  pinMode(motion_sensor, INPUT);
  pinMode(led, OUTPUT);

  connectToWiFi();
  checkTCPConnection();

  client.print("esp32");

  DS18B20.requestTemperatures();

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
    light = analogRead(light_sensor);

    if (newTime >= transmissionTime)
    {
      temp = getTemperature();
      tcpTime = millis();
    }

    char buff[100];
    snprintf(buff, 100, "{ \"motion\": %d, \"light\": %d, \"temperature\": %f }", currentMotion, light, temp);
    client.print(buff);

    lastSentLedState = currentMotion;
  }

  delay(50);
}

float getTemperature()
{
  temp = DS18B20.getTempCByIndex(0);
  DS18B20.requestTemperatures();
  return temp;
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