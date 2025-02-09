#include <WiFi.h>
#include <OneWire.h>
#include <DallasTemperature.h>

const char WIFI_SSID[] = "Jonny Jr";
const char WIFI_PASSWORD[] = "ProcioneSpazialeMistico";
const char CORTANA_IP[] = "192.168.178.117";
const int CORTANA_PORT = 5116;

const int led = 33;
const int light_sensor = 25;
const int motion_sensor = 26;
const int temp_sensor = 27;

OneWire oneWire(temp_sensor);
DallasTemperature DS18B20(&oneWire);
WiFiClient client;

int currentMotion = LOW;
int lastMotion = LOW;
float temp;
int light;

unsigned long tcpTime;
unsigned long valueTime;
const int transmissionTime = 2000;
const int updateTime = 1000;

void setup() {
  analogSetAttenuation(ADC_11db);
  DS18B20.begin();

  pinMode(motion_sensor, INPUT);
  pinMode(led, OUTPUT);

  connectToWiFi();
  connectToCortana();

  client.print("esp32");
  readSensors();

  tcpTime = millis();
  valueTime = millis();
}

void loop() {
  currentMotion = digitalRead(motion_sensor);
  digitalWrite(led, currentMotion);
 
  if((millis() - tcpTime >= transmissionTime) || (currentMotion != lastMotion))
  {
    char buff[100];
    snprintf(buff, 100, "{ \"motion\": %d, \"light\": %d, \"temperature\": %f }", currentMotion, light, temp);
    client.print(buff);

    lastMotion = currentMotion;

    tcpTime = millis();
  }

  if(millis() - valueTime >= updateTime)
  {
    connectToCortana();
    readSensors();

    valueTime = millis();
  }

  delay(100);
}

void readSensors()
{
  DS18B20.requestTemperatures();
  temp = DS18B20.getTempCByIndex(0);
  light = analogRead(light_sensor);
}

void connectToCortana()
{ 
  if(!client.connected())
  {
    while (!client.connect(CORTANA_IP, CORTANA_PORT)) {
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
    
    if(client.connected()) client.stop();
    WiFi.disconnect();
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    WiFi.setSleep(false);
    tryCount++;
    delay(1500);
  }

  delay(500);
}

