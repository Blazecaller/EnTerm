namespace ENTestTerminal;

public enum AppLanguage
{
    Turkish,
    English,
}

public static class Loc
{
    public static AppLanguage Current { get; private set; } = AppLanguage.Turkish;

    public static void Toggle() => Current = Current == AppLanguage.Turkish ? AppLanguage.English : AppLanguage.Turkish;

    public static string T(string key) => Map.TryGetValue(key, out var v) ? (Current == AppLanguage.Turkish ? v.Tr : v.En) : key;

    private static readonly Dictionary<string, (string Tr, string En)> Map = new()
    {
        ["conn.title"] = ("Bağlantı", "Connection"),
        ["conn.port"] = ("Port:", "Port:"),
        ["conn.refresh"] = ("Yenile", "Refresh"),
        ["conn.baud"] = ("Baud:", "Baud:"),
        ["conn.parity"] = ("Parite:", "Parity:"),
        ["conn.databits"] = ("Data Biti:", "Data bits:"),
        ["conn.stopbits"] = ("Stop Biti:", "Stop bits:"),
        ["conn.connect"] = ("Bağlan", "Connect"),
        ["conn.disconnect"] = ("Bağlantıyı Kes", "Disconnect"),
        ["conn.statusHelp"] = (
            "Bağlantı durumu:\n" +
            "• Kırmızı -> Bağlı değil.\n" +
            "• Yeşil -> Bağlı, cihaz normal yanıt veriyor.\n" +
            "• Turuncu -> Bağlı, ancak cihazdan yanıt gelmiyor. (baud yanlış veya güç yok).\n" +
            "   Uygulama otomatik olarak yeniden bağlanmayı deniyor.\n"+
            "<Blazecaller-2026> 52",
            "Connection status:\n" +
            "• Red — Disconnected.\n" +
            "• Green — Connected, device responding normally.\n" +
            "• Orange — Connected, but no signal from the device (wrong baud rate or power loss).\n" +
            "   The app is automatically retrying the handshake.\n"+
            "<Blazecaller-2026> 52"),

        ["parity.none"] = ("Yok", "None"),
        ["parity.odd"] = ("Tek", "Odd"),
        ["parity.even"] = ("Çift", "Even"),
        ["parity.mark"] = ("İşaret", "Mark"),
        ["parity.space"] = ("Boşluk", "Space"),

        ["basic.title"] = ("Komutlar", "Commands"),
        ["basic.sensor"] = ("Sensor:", "Sensor:"),
        ["basic.apply"] = ("Uygula", "Apply"),
        ["basic.on"] = ("AÇIK", "ON"),
        ["basic.off"] = ("KAPALI", "OFF"),
        ["basic.serialLabel"] = ("Seri No:", "Serial No:"),
        ["basic.setSerial"] = ("Seri No Ayarla", "Set Serial Number"),
        ["basic.setBaud"] = ("Baud Kaydet", "Save Baud Rate"),
        ["basic.showSensorList"] = ("Sensor Listesi", "Show Sensor List"),
        ["basic.systemStatus"] = ("Sistem Durumu", "System Status"),
        ["basic.help"] = ("Yardım", "Help"),
        ["basic.advancedShow"] = ("Terminal ▶", "Terminal ▶"),
        ["basic.advancedHide"] = ("Terminal ◀", "Terminal ◀"),

        ["sensor.digitalTemp"] = ("Dijital Sıcaklık", "Digital Temp"),
        ["sensor.solar"] = ("Güneş", "Solar"),
        ["sensor.soil"] = ("Toprak", "Soil"),
        ["sensor.rain"] = ("Yağmur", "Rain"),
        ["sensor.bgtWind"] = ("BGT Rüzgar", "BGT Wind"),

        ["nav.calibration"] = ("Kalibrasyon Modu →", "Calibration Mode →"),
        ["nav.back"] = ("← Normal Mod", "← Standard Mode"),

        ["grp.tempHumidity"] = ("Sıcaklık ve Nem", "Temperature & Humidity"),
        ["grp.windVibration"] = ("Rüzgar ve Titreşim", "Wind & Vibration"),
        ["grp.powerStatus"] = ("Güç ve Cihaz Durumu", "Power & Device Status"),
        ["grp.zeroSpeedPwm"] = ("Rüzgar PWM", "Wind PWM"),
        ["grp.roomTempPwm"] = ("Oda Sıcaklığı PWM ve Ayar Noktası", "Wind Room-Temp PWM & Setpoint"),
        ["grp.thresholds"] = ("Eşikler ve Zamanlama", "Thresholds & Timing"),
        ["grp.globalStatus"] = ("Ortam Durumu", "Ambient Status"),
        ["grp.sensorLinks"] = ("Haberleşme Durumu", "Communication Status"),

        ["tile.digitalTemp"] = ("Dijital Sıcaklık", "Digital Temp"),
        ["tile.sht20Temp"] = ("SHT20 Sıcaklık", "SHT20 Temp"),
        ["tile.sht20Humidity"] = ("SHT20 Nem", "SHT20 Humidity"),
        ["tile.windSpeed"] = ("Rüzgar Hızı", "Wind Speed"),
        ["tile.accelTotalG"] = ("Titreşim", "Vibration"),
        ["tile.windPwmOutput"] = ("Rüzgar PWM Çıkışı", "Wind PWM Output"),
        ["tile.batteryLevel"] = ("Batarya Seviyesi", "Battery Level"),
        ["tile.version"] = ("Yazılım Sürümü", "Firmware Version"),
        ["tile.zeroSpeedPwm1"] = ("PWM1", "PWM1"),
        ["tile.zeroSpeedPwm2"] = ("PWM2", "PWM2"),
        ["tile.zeroSpeedPwm3"] = ("PWM3", "PWM3"),
        ["tile.roomTempPwm1"] = ("Oda Sıcaklığı PWM 1", "Room-Temp PWM 1"),
        ["tile.roomTempPwm3"] = ("Oda Sıcaklığı PWM 3", "Room-Temp PWM 3"),
        ["tile.windSpeedSetpoint"] = ("Rüzgar Hızı Set Değeri", "Wind Speed Setpoint"),
        ["tile.accelThreshold"] = ("Titreşim Eşiği", "Tilt Threshold"),
        ["tile.sleepInterval"] = ("Uyku Aralığı", "Sleep Interval"),

        ["tile.statusTemp"] = ("Sıcaklık", "Temperature"),
        ["tile.statusBattery"] = ("Batarya", "Battery"),
        ["tile.statusVibration"] = ("Titreşim", "Vibration"),
        ["tile.statusWind"] = ("Rüzgar", "Wind"),
        ["tile.statusDeltaTemp"] = ("Delta Sıcaklık", "Delta Temp"),

        ["tile.linkAmbientTemp"] = ("Ortam Sıcaklığı (SPI)", "Ambient Temp (SPI)"),
        ["tile.linkHeaterTemp"] = ("Isıtıcı Sıcaklığı (SPI)", "Heater Temp (SPI)"),
        ["tile.linkAccel"] = ("Titreşim (I2C)", "Tilt (I2C)"),
        ["tile.linkSht20"] = ("SHT20 (I2C)", "SHT20 (I2C)"),
        ["tile.linkDigitalTemp"] = ("Dijital Sıcaklık (ID5)", "Digital Temp (ID5)"),
        ["tile.linkSolar"] = ("Güneş (ID6)", "Solar (ID6)"),
        ["tile.linkSoil"] = ("Toprak (ID7)", "Soil (ID7)"),
        ["tile.linkRain"] = ("Yağmur (ID8)", "Rain (ID8)"),
        ["tile.linkWind"] = ("Anemometre (ID9)", "Anemometer (ID9)"),

        ["status.high"] = ("Yüksek", "HIGH"),
        ["status.low"] = ("Düşük", "LOW"),
        ["status.linkOk"] = ("Var", "OK"),

        ["unit.min"] = ("dk", "min"),

        ["status.disconnected"] = ("Bağlı Değil", "Disconnected"),
        ["status.connected"] = ("Bağlı", "Connected"),
        ["status.noSignal"] = ("Sinyal Yok", "No Signal"),
        ["status.mode"] = ("Mod", "Mode"),
        ["status.modeUnknown"] = ("Bilinmiyor", "Unknown"),
        ["status.modeData"] = ("Veri", "Data"),
        ["status.modeCommand"] = ("Komut", "Command"),
        ["status.test"] = ("Test", "Test"),
        ["status.on"] = ("Açık", "On"),
        ["status.off"] = ("Kapalı", "Off"),
        ["status.frames"] = ("Veri Sayısı", "Frames"),

        ["popup.sensorList"] = ("Sensor Listesi", "Sensor List"),
        ["popup.status"] = ("Sistem Durumu", "System Status"),
        ["popup.help"] = ("Yardım", "Help"),
        ["popup.sensorControl"] = ("Sensor Kontrolü", "Sensor Control"),
        ["popup.serialNo"] = ("Seri No", "Serial No"),
        ["popup.baudRate"] = ("Baud Hızı", "Baud Rate"),
        ["msg.commError"] = ("Cihazdan geçerli bir yanıt alınamadı.", "Could not get a valid response from the device."),
        ["msg.noPort"] = ("Önce bir COM portu seçin.", "Select a COM port first."),
        ["msg.noPortTitle"] = ("Port Seçilmedi", "No port selected"),
        ["msg.invalidBaud"] = ("Baud hızı pozitif bir sayı olmalıdır.", "Baud rate must be a positive number."),
        ["msg.invalidBaudTitle"] = ("Geçersiz Baud Hızı", "Invalid baud rate"),
        ["msg.connFailedTitle"] = ("Bağlantı Başarısız", "Connection failed"),
        ["msg.invalidSerial"] = ("Seri numarası sayısal olmalıdır.", "Serial number must be numeric."),
        ["msg.invalidSerialTitle"] = ("Geçersiz Seri No", "Invalid serial number"),
        ["msg.handshakeFailed"] = ("Komut moduna girilemedi, bağlantı açık kalıyor.", "Could not confirm command mode; connection stays open."),
    };
}
